using System.Buffers.Binary;
using System.IO;
using System.IO.MemoryMappedFiles;
using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Primitives;

namespace NetCraft.Storage;

//MCA区域文件对应原版RegionFile
//管理单个.mca文件的扇区分配与chunk读写，支持内部存储和外部.mcc大块
//优化点2.8：读取路径走MemoryMappedFile避免FileStream多次拷贝（开关RegionFileMemoryMapped）
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
    //MemoryMappedFile只读视图懒加载对应优化点2.8
    //首次读取时创建避免空Region产生MMF开销
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

    //读取chunk的解压流，chunk不存在或损坏返回null，调用方负责close返回的BinaryReader
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

    //用MemoryMappedFile读取chunk数据对应优化点2.8
    //大文件场景避免FileStream多次拷贝直接映射sector读取
    //外部.mcc大块仍走FileStream因MMF对临时小文件收益低
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
            //MMF视图按需扩展避免越界访问
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

    //懒加载MemoryMappedFile只读视图对应优化点2.8
    //首次读取时按当前文件长度创建MMF避免空Region产生开销
    private MemoryMappedFile GetOrCreateMemoryMappedView()
    {
        if (_mmf is not null) return _mmf;
        if (!_mmfInit)
        {
            long fileLength = _file.Length;
            if (fileLength < HeaderSize)
            {
                //文件过小直接用FileStream读取路径
                _mmfInit = true;
                throw new InvalidOperationException("Region file too small for MMF");
            }
            //leaveOpen必须为true MMF释放时不能关底下的FileStream
            //写区块后InvalidateMemoryMappedView会Dispose MMF leaveOpen false会把_file连带关闭 后续写入全部ObjectDisposed
            _mmf = MemoryMappedFile.CreateFromFile(_file, null, fileLength, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
            _mmfInit = true;
        }
        if (_mmf is null) throw new InvalidOperationException("Region file too small for MMF");
        return _mmf;
    }

    //写入后使MMF失效避免文件扩展后视图越界对应优化点2.8
    //下次读取时按最新文件长度重建MMF
    private void InvalidateMemoryMappedView()
    {
        _mmf?.Dispose();
        _mmf = null;
        _mmfInit = false;
    }

    //写入chunk的压缩流，close时把数据回写到region文件
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

    //清除chunk并释放扇区，同时删除对应的外部文件
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

    //探测chunk是否存在且头合法，不解析完整数据
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

    //写入chunk数据，自动分配扇区或重用旧扇区，超大chunk走外部.mcc文件
    internal void WriteChunk(ChunkPos pos, ReadOnlySpan<byte> data)
    {
        //文件已关闭时给出带路径的明确错误 替代FileStream的裸ObjectDisposedException
        if (!_file.CanWrite)
            throw new ObjectDisposedException(nameof(RegionFile), $"region 文件已关闭 pos={pos} path={_path}");
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

    //外部chunk的stub头，length=1，versionId带128标志
    private byte[] CreateExternalStub()
    {
        byte[] stub = new byte[ChunkHeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(stub, 1);
        stub[4] = (byte)(_version.Id | ExternalStreamFlag);
        return stub;
    }

    //写大chunk到外部.mcc，先写临时文件再原子move，跳过5字节MCA头
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

    //文件尾部补齐到完整扇区
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
            //记录关闭来源 若关闭后仍被写入会抛ObjectDisposed 该日志用于定位是谁关的文件
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

    //chunk写入缓冲对应原版ChunkBuffer
    //继承MemoryStream，前5字节为MCA头占位，Dispose时回填streamLength并写回region
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
