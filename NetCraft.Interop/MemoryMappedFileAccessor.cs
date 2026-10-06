using System.IO.MemoryMappedFiles;

namespace NetCraft.Interop;

//MemoryMappedFileAccessor cross-platform memory-mapped file accessor
//Wraps the vanilla .NET MemoryMappedFile to provide a unified access interface
//Platform-specific P/Invoke is forbidden, use the .NET cross-platform MemoryMappedFile API
public sealed class MemoryMappedFileAccessor : IDisposable
{
    //Underlying MMF handle
    private readonly MemoryMappedFile _mmf;
    //Underlying file stream (closed on Dispose if held)
    private readonly FileStream? _fileStream;
    //Whether already disposed
    private bool _disposed;

    //FromFile creates a memory mapping from a file path
    public static MemoryMappedFileAccessor FromFile(string path, long capacity, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
    {
        var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (fs.Length < capacity)
        {
            fs.SetLength(capacity);
        }
        var mmf = MemoryMappedFile.CreateFromFile(fs, null, capacity, access, HandleInheritability.None, false);
        return new MemoryMappedFileAccessor(mmf, fs);
    }

    //CreateNew creates a non-persistent memory mapping (not backed by a file on disk)
    public static MemoryMappedFileAccessor CreateNew(string? mapName, long capacity)
    {
        var mmf = MemoryMappedFile.CreateNew(mapName, capacity);
        return new MemoryMappedFileAccessor(mmf, null);
    }

    private MemoryMappedFileAccessor(MemoryMappedFile mmf, FileStream? fileStream)
    {
        _mmf = mmf;
        _fileStream = fileStream;
    }

    //CreateViewStream creates a view stream for the given offset and length
    public MemoryMappedViewStream CreateViewStream(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        => _mmf.CreateViewStream(offset, size, access);

    //CreateViewAccessor creates a view accessor for the given offset and length
    public MemoryMappedViewAccessor CreateViewAccessor(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        => _mmf.CreateViewAccessor(offset, size, access);

    //CreateViewSpan creates a readable/writable Span view
    public unsafe Span<byte> CreateViewSpan(long offset, int size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
    {
        var accessor = _mmf.CreateViewAccessor(offset, size, access);
        byte* ptr = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        return new Span<byte>(ptr, size);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _mmf.Dispose();
        _fileStream?.Dispose();
        _disposed = true;
    }
}
