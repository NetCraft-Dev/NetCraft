using System.Buffers.Binary;
using System.IO;
using System.IO.MemoryMappedFiles;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//MCA region file, maps to vanilla RegionFile
//Manages sector allocation and chunk read/write for a single .mca file, supporting internal storage and external .mcc blobs
//Optimization 2.8: the read path uses a MemoryMappedFile to avoid repeated FileStream copies (RegionFileMemoryMapped switch)
public sealed class RegionFile : IDisposable
{
    private const int SectorBytes = 4096;
    private const int SectorInts = 1024;
    private const int ChunkHeaderSize = 5;
    private const string ExternalFileExtension = ".mcc";
    private const byte ExternalStreamFlag = 128;
    private const int ExternalChunkThreshold = 256;
    private const int HeaderSize = SectorBytes * 2;

    private readonly RegionStorageInfo _info;
    private readonly string _path;
    private readonly FileStream _file;
    private readonly string _externalFileDir;
    private readonly RegionFileVersion _version;
    private readonly byte[] _header = new byte[HeaderSize];
    private readonly RegionBitmap _usedSectors = new();
    private readonly object _gate = new();
    //Read-only MemoryMappedFile view, lazily loaded, optimization 2.8
    //Created on first read so an empty region does not incur MMF overhead
    private MemoryMappedFile? _mmf;
    private bool _mmfInit;

    public RegionFile(RegionStorageInfo info, string path, string externalFileDir, bool sync)
        : this(info, path, externalFileDir, RegionFileVersion.GetSelected(), sync)
    {
    }

    public RegionFile(RegionStorageInfo info, string path, string externalFileDir, RegionFileVersion version, bool sync)
    {
        _info = info;
        _path = path;
        _version = version;
        if (!Directory.Exists(externalFileDir))
            throw new ArgumentException($"Expected directory, got {System.IO.Path.GetFullPath(externalFileDir)}");
        _externalFileDir = externalFileDir;

        var options = sync ? FileOptions.WriteThrough : FileOptions.None;
        _file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 8192, options);

        _usedSectors.Force(0, 2);
        int readHeaderBytes = _file.Read(_header, 0, HeaderSize);
        if (readHeaderBytes > 0)
        {
            if (readHeaderBytes != HeaderSize)
                Log.Warning($"Region file {_path} has truncated header: {readHeaderBytes}");
            long size = _file.Length;
            for (int i = 0; i < SectorInts; i++)
            {
                int offset = GetOffset(i);
                if (offset == 0) continue;
                int sectorNumber = GetSectorNumber(offset);
                int numSectors = GetNumSectors(offset);
                if (sectorNumber < 2)
                {
                    Log.Warning($"Region file {_path} has invalid sector at index {i}; sector {sectorNumber} overlaps with header");
                    SetOffset(i, 0);
                }
                else if (numSectors == 0)
                {
                    Log.Warning($"Region file {_path} has an invalid sector at index {i}; size has to be > 0");
                    SetOffset(i, 0);
                }
                else if ((long)sectorNumber * SectorBytes > size)
                {
                    Log.Warning($"Region file {_path} has an invalid sector at index {i}; sector {sectorNumber} is out of bounds");
                    SetOffset(i, 0);
                }
                else
                {
                    _usedSectors.Force(sectorNumber, numSectors);
                }
            }
        }
    }

    public string Path => _path;

    //Read the chunk's decompressed stream; returns null when the chunk is absent or corrupt, the caller must close the returned BinaryReader
    public BinaryReader? GetChunkDataInputStream(ChunkPos pos)
    {
        lock (_gate)
        {
            int offset = GetOffset(GetOffsetIndex(pos));
            if (offset == 0) return null;
            int sectorNumber = GetSectorNumber(offset);
            int numSectors = GetNumSectors(offset);
            int sectorsLength = numSectors * SectorBytes;
            byte[] buffer = new byte[sectorsLength];
            _file.Position = sectorNumber * SectorBytes;
            int read = _file.Read(buffer, 0, sectorsLength);
            if (read < ChunkHeaderSize)
            {
                Log.Error($"Chunk {pos} header is truncated: expected {sectorsLength} but read {read}");
                return null;
            }
            int length = BinaryPrimitives.ReadInt32BigEndian(buffer);
            byte versionId = buffer[4];
            if (length == 0)
            {
                Log.Warning($"Chunk {pos} is allocated, but the stream is missing");
                return null;
            }
            int streamLength = length - 1;
            if (IsExternalStreamChunk(versionId))
            {
                if (streamLength != 0)
                    Log.Warning("Chunk has both internal and external streams");
                return CreateExternalChunkInputStream(pos, GetExternalChunkVersion(versionId));
            }
            if (streamLength > read - ChunkHeaderSize)
            {
                Log.Error($"Chunk {pos} stream is truncated: expected {streamLength} but read {read - ChunkHeaderSize}");
                return null;
            }
            if (streamLength < 0)
            {
                Log.Error($"Declared size {length} of chunk {pos} is negative");
                return null;
            }
            return CreateChunkInputStream(pos, versionId, new MemoryStream(buffer, ChunkHeaderSize, streamLength, false));
        }
    }

    //Read chunk data with a MemoryMappedFile, optimization 2.8
    //For large files, avoid repeated FileStream copies and map the sector directly
    //External .mcc blobs still use FileStream since MMF yields little for temporary small files
    public BinaryReader? GetChunkDataInputStreamWithMemoryMapped(ChunkPos pos)
    {
        if (!OptimizationFlags.RegionFileMemoryMapped) return GetChunkDataInputStream(pos);
        lock (_gate)
        {
            int offset = GetOffset(GetOffsetIndex(pos));
            if (offset == 0) return null;
            int sectorNumber = GetSectorNumber(offset);
            int numSectors = GetNumSectors(offset);
            int sectorsLength = numSectors * SectorBytes;
            var viewStream = GetOrCreateMemoryMappedView();
            //The MMF view is extended on demand to avoid out-of-bounds access
            long fileLength = _file.Length;
            long sectorStart = (long)sectorNumber * SectorBytes;
            int readLength = (int)Math.Min(sectorsLength, fileLength - sectorStart);
            if (readLength < ChunkHeaderSize)
            {
                Log.Error($"Chunk {pos} header is truncated via MMF: expected {sectorsLength} but file has {fileLength - sectorStart}");
                return null;
            }
            byte[] buffer = new byte[readLength];
            using (var accessor = viewStream.CreateViewAccessor(sectorStart, readLength, MemoryMappedFileAccess.Read))
            {
                accessor.ReadArray(0L, buffer, 0, readLength);
            }
            int length = BinaryPrimitives.ReadInt32BigEndian(buffer);
            byte versionId = buffer[4];
            if (length == 0)
            {
                Log.Warning($"Chunk {pos} is allocated, but the stream is missing");
                return null;
            }
            int streamLength = length - 1;
            if (IsExternalStreamChunk(versionId))
            {
                if (streamLength != 0)
                    Log.Warning("Chunk has both internal and external streams");
                return CreateExternalChunkInputStream(pos, GetExternalChunkVersion(versionId));
            }
            if (streamLength > readLength - ChunkHeaderSize)
            {
                Log.Error($"Chunk {pos} stream is truncated via MMF: expected {streamLength} but read {readLength - ChunkHeaderSize}");
                return null;
            }
            if (streamLength < 0)
            {
                Log.Error($"Declared size {length} of chunk {pos} is negative");
                return null;
            }
            return CreateChunkInputStream(pos, versionId, new MemoryStream(buffer, ChunkHeaderSize, streamLength, false));
        }
    }

    //Lazily load the read-only MemoryMappedFile view, optimization 2.8
    //Create the MMF at the current file length on first read so an empty region incurs no overhead
    private MemoryMappedFile GetOrCreateMemoryMappedView()
    {
        if (_mmf is not null) return _mmf;
        if (!_mmfInit)
        {
            long fileLength = _file.Length;
            if (fileLength < HeaderSize)
            {
                //File too small, use the FileStream read path directly
                _mmfInit = true;
                throw new InvalidOperationException("Region file too small for MMF");
            }
            //leaveOpen must be true; releasing the MMF must not close the underlying FileStream
            //After writing a chunk, InvalidateMemoryMappedView disposes the MMF; with leaveOpen false it would close _file too, making every later write ObjectDisposed
            _mmf = MemoryMappedFile.CreateFromFile(_file, null, fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
            _mmfInit = true;
        }
        if (_mmf is null) throw new InvalidOperationException("Region file too small for MMF");
        return _mmf;
    }

    //Invalidate the MMF after a write so the view does not go out of bounds as the file grows, optimization 2.8
    //Rebuild the MMF at the latest file length on the next read
    private void InvalidateMemoryMappedView()
    {
        _mmf?.Dispose();
        _mmf = null;
        _mmfInit = false;
    }

    //Write the chunk's compressed stream; on close the data is written back to the region file
    public BinaryWriter GetChunkDataOutputStream(ChunkPos pos)
    {
        var buffer = new ChunkDataBuffer(this, pos, _version);
        Stream compressed = _version.WrapOutput(buffer);
        return new BinaryWriter(compressed);
    }

    public void Flush()
    {
        lock (_gate)
        {
            _file.Flush(true);
        }
    }

    //Clear the chunk and free its sectors, also deleting the matching external file
    public void Clear(ChunkPos pos)
    {
        lock (_gate)
        {
            int offsetIndex = GetOffsetIndex(pos);
            int offset = GetOffset(offsetIndex);
            if (offset == 0) return;
            SetOffset(offsetIndex, 0);
            SetTimestamp(offsetIndex, GetTimestamp());
            WriteHeader();
            try { File.Delete(GetExternalChunkPath(pos)); }
            catch (IOException) { }
            _usedSectors.Free(GetSectorNumber(offset), GetNumSectors(offset));
            InvalidateMemoryMappedView();
        }
    }

    //Probe whether the chunk exists with a valid header, without parsing the full data
    public bool DoesChunkExist(ChunkPos pos)
    {
        lock (_gate)
        {
            int offset = GetOffset(GetOffsetIndex(pos));
            if (offset == 0) return false;
            int sectorNumber = GetSectorNumber(offset);
            int numSectors = GetNumSectors(offset);
            byte[] streamHeader = new byte[ChunkHeaderSize];
            _file.Position = sectorNumber * SectorBytes;
            int read = _file.Read(streamHeader, 0, ChunkHeaderSize);
            if (read != ChunkHeaderSize) return false;
            int length = BinaryPrimitives.ReadInt32BigEndian(streamHeader);
            byte versionId = streamHeader[4];
            if (IsExternalStreamChunk(versionId))
            {
                if (!RegionFileVersion.IsValidVersion(GetExternalChunkVersion(versionId))) return false;
                return File.Exists(GetExternalChunkPath(pos));
            }
            if (!RegionFileVersion.IsValidVersion(versionId)) return false;
            if (length == 0) return false;
            int streamLength = length - 1;
            if (streamLength < 0 || streamLength > SectorBytes * numSectors) return false;
            return true;
        }
    }

    public bool HasChunk(ChunkPos pos) => GetOffset(GetOffsetIndex(pos)) != 0;

    //Write chunk data, allocating sectors automatically or reusing old ones; oversized chunks go to an external .mcc file
    internal void WriteChunk(ChunkPos pos, ReadOnlySpan<byte> data)
    {
        //Give a clear error with the path when the file is closed, replacing the bare ObjectDisposedException from FileStream
        if (!_file.CanWrite)
            throw new ObjectDisposedException(nameof(RegionFile), $"region file is closed pos={pos} path={_path}");
        lock (_gate)
        {
            int offsetIndex = GetOffsetIndex(pos);
            int offset = GetOffset(offsetIndex);
            int sectorNumber = GetSectorNumber(offset);
            int currentSectorCount = GetNumSectors(offset);
            int dataSize = data.Length;
            int sectorsNeeded = SizeToSectors(dataSize);
            int newSectorNumber;
            if (sectorsNeeded >= ExternalChunkThreshold)
            {
                string externalChunkPath = GetExternalChunkPath(pos);
                Log.Warning($"Saving oversized chunk {pos} ({dataSize} bytes) to external file {externalChunkPath}");
                sectorsNeeded = 1;
                newSectorNumber = _usedSectors.Allocate(1);
                WriteToExternalFile(externalChunkPath, data);
                byte[] stub = CreateExternalStub();
                _file.Position = newSectorNumber * SectorBytes;
                _file.Write(stub, 0, stub.Length);
            }
            else
            {
                newSectorNumber = _usedSectors.Allocate(sectorsNeeded);
                _file.Position = newSectorNumber * SectorBytes;
                _file.Write(data);
                try { File.Delete(GetExternalChunkPath(pos)); }
                catch (IOException) { }
            }
            SetOffset(offsetIndex, PackSectorOffset(newSectorNumber, sectorsNeeded));
            SetTimestamp(offsetIndex, GetTimestamp());
            WriteHeader();
            if (sectorNumber != 0)
                _usedSectors.Free(sectorNumber, currentSectorCount);
            InvalidateMemoryMappedView();
        }
    }

    //The stub header for an external chunk: length=1, versionId with the 128 flag
    private byte[] CreateExternalStub()
    {
        byte[] stub = new byte[ChunkHeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(stub, 1);
        stub[4] = (byte)(_version.Id | ExternalStreamFlag);
        return stub;
    }

    //Write a big chunk to an external .mcc: write a temp file then atomically move, skipping the 5-byte MCA header
    private void WriteToExternalFile(string targetPath, ReadOnlySpan<byte> data)
    {
        string tmpPath = System.IO.Path.Combine(_externalFileDir, "tmp-" + Guid.NewGuid().ToString("N") + ExternalFileExtension);
        using (var fs = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 8192, FileOptions.WriteThrough))
        {
            fs.Write(data.Slice(ChunkHeaderSize));
        }
        File.Move(tmpPath, targetPath, overwrite: true);
    }

    private BinaryReader? CreateChunkInputStream(ChunkPos pos, byte versionId, Stream chunkStream)
    {
        var version = RegionFileVersion.FromId(versionId);
        if (version == RegionFileVersion.VersionCustom)
        {
            Log.Error("Custom region compression is not supported");
            return null;
        }
        if (version == null)
        {
            Log.Error($"Chunk {pos} has invalid chunk stream version {versionId}");
            return null;
        }
        return new BinaryReader(version.WrapInput(chunkStream));
    }

    private BinaryReader? CreateExternalChunkInputStream(ChunkPos pos, byte versionId)
    {
        string externalFile = GetExternalChunkPath(pos);
        if (!File.Exists(externalFile))
        {
            Log.Error($"External chunk path {externalFile} is not a file");
            return null;
        }
        return CreateChunkInputStream(pos, versionId, new FileStream(externalFile, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    private string GetExternalChunkPath(ChunkPos pos) => System.IO.Path.Combine(_externalFileDir, $"c.{pos.X}.{pos.Z}{ExternalFileExtension}");

    private void WriteHeader()
    {
        _file.Position = 0;
        _file.Write(_header, 0, HeaderSize);
    }

    //Pad the file tail to a full sector
    private void PadToFullSector()
    {
        int fileSize = (int)_file.Length;
        int paddedSize = SizeToSectors(fileSize) * SectorBytes;
        if (fileSize != paddedSize)
        {
            _file.Position = paddedSize - 1;
            _file.Write(new byte[1], 0, 1);
        }
    }

    private int GetOffset(int index) => BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(index * 4));
    private void SetOffset(int index, int value) => BinaryPrimitives.WriteInt32BigEndian(_header.AsSpan(index * 4), value);
    private int GetTimestamp(int index) => BinaryPrimitives.ReadInt32BigEndian(_header.AsSpan(SectorBytes + index * 4));
    private void SetTimestamp(int index, int value) => BinaryPrimitives.WriteInt32BigEndian(_header.AsSpan(SectorBytes + index * 4), value);
    private static int GetOffsetIndex(ChunkPos pos) => pos.GetRegionLocalX() + (pos.GetRegionLocalZ() * 32);

    private static int PackSectorOffset(int index, int size) => (index << 8) | size;
    private static int GetNumSectors(int offset) => offset & 0xFF;
    private static int GetSectorNumber(int offset) => (offset >> 8) & 0xFFFFFF;
    private static int SizeToSectors(int size) => (size + SectorBytes - 1) / SectorBytes;
    private static int GetTimestamp() => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    private static bool IsExternalStreamChunk(byte version) => (version & ExternalStreamFlag) != 0;
    private static byte GetExternalChunkVersion(byte version) => (byte)(version & ~ExternalStreamFlag);

    public void Close()
    {
        lock (_gate)
        {
            //Record the close source; a write after close throws ObjectDisposed and this log helps locate who closed the file
            Log.Debug($"Region file closed unexpectedly: {_path}{Environment.NewLine}{Environment.StackTrace}");
            PadToFullSector();
            _file.Flush(true);
            InvalidateMemoryMappedView();
            _file.Close();
        }
    }

    public void Dispose()
    {
        Close();
    }

    //Chunk write buffer, maps to vanilla ChunkBuffer
    //Extends MemoryStream; the first 5 bytes are an MCA header placeholder, Dispose fills in streamLength and writes back to the region
    private sealed class ChunkDataBuffer : MemoryStream
    {
        private readonly RegionFile _owner;
        private readonly ChunkPos _pos;
        private bool _written;

        public ChunkDataBuffer(RegionFile owner, ChunkPos pos, RegionFileVersion version)
        {
            _owner = owner;
            _pos = pos;
            byte[] header = new byte[ChunkHeaderSize];
            header[4] = (byte)version.Id;
            Write(header, 0, ChunkHeaderSize);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_written)
            {
                _written = true;
                int streamLength = (int)Length - ChunkHeaderSize + 1;
                byte[] buf = GetBuffer();
                BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0, 4), streamLength);
                _owner.WriteChunk(_pos, buf.AsSpan(0, (int)Length));
            }
            base.Dispose(disposing);
        }
    }
}
