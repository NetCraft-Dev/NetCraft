namespace NetCraft.Util;

//DummyFileAttributes placeholder attributes for the virtual filesystem, maps to vanilla net.minecraft.util.DummyFileAttributes
//Time is always Unix epoch, size and file key are always empty; only distinguishes directory from file
public abstract class DummyFileAttributes
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.FromUnixTimeMilliseconds(0);

    //Directory directory placeholder attributes
    public static readonly DummyFileAttributes Directory = new DirectoryEntry();

    //File file placeholder attributes
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
