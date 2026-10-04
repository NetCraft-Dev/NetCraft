namespace NetCraft.Util;

//DummyFileAttributes 虚拟文件系统的占位属性 对应原版 net.minecraft.util.DummyFileAttributes
//时间统一取 Unix 纪元 大小与文件键恒为空 只区分目录与文件两种
public abstract class DummyFileAttributes
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.FromUnixTimeMilliseconds(0);

    //Directory 目录占位属性
    public static readonly DummyFileAttributes Directory = new DirectoryEntry();

    //File 文件占位属性
    public static readonly DummyFileAttributes File = new FileEntry();

    public DateTimeOffset LastModifiedTime => Epoch;

    public DateTimeOffset LastAccessTime => Epoch;

    public DateTimeOffset CreationTime => Epoch;

    public bool IsSymbolicLink => false;

    public bool IsOther => false;

    public long Size => 0L;

    public object? FileKey => null;

    public abstract bool IsRegularFile { get; }

    public abstract bool IsDirectory { get; }

    private sealed class DirectoryEntry : DummyFileAttributes
    {
        public override bool IsRegularFile => false;

        public override bool IsDirectory => true;
    }

    private sealed class FileEntry : DummyFileAttributes
    {
        public override bool IsRegularFile => true;

        public override bool IsDirectory => false;
    }
}
