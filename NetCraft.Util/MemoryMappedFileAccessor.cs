using System.IO.MemoryMappedFiles;

namespace NetCraft.Util;

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

    private MemoryMappedFileAccessor(MemoryMappedFile mmf, FileStream? fileStream)
    {
        _mmf = mmf;
        _fileStream = fileStream;
    }

    //CreateViewStream creates a view stream for the given offset and length
    public MemoryMappedViewStream CreateViewStream(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        => _mmf.CreateViewStream(offset, size, access);

    public void Dispose()
    {
        if (_disposed) return;
        _mmf.Dispose();
        _fileStream?.Dispose();
        _disposed = true;
    }
}
