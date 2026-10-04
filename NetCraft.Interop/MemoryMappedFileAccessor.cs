using System.IO.MemoryMappedFiles;

namespace NetCraft.Interop;

//MemoryMappedFileAccessor 跨平台内存映射文件访问器
//对应原版 .NET MemoryMappedFile 封装统一访问接口
//禁止平台特定 P/Invoke 用 .NET 跨平台 MemoryMappedFile API
public sealed class MemoryMappedFileAccessor : IDisposable
{
    //底层 MMF 句柄
    private readonly MemoryMappedFile _mmf;
    //底层文件流（如果持有则随 Dispose 关闭）
    private readonly FileStream? _fileStream;
    //是否已释放
    private bool _disposed;

    //FromFile 从文件路径创建内存映射
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

    //CreateNew 创建非持久化内存映射（不与磁盘文件关联）
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

    //CreateViewStream 创建指定偏移和长度的视图流
    public MemoryMappedViewStream CreateViewStream(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        => _mmf.CreateViewStream(offset, size, access);

    //CreateViewAccessor 创建指定偏移和长度的视图访问器
    public MemoryMappedViewAccessor CreateViewAccessor(long offset, long size, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        => _mmf.CreateViewAccessor(offset, size, access);

    //CreateViewSpan 创建可读写的 Span 视图
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
