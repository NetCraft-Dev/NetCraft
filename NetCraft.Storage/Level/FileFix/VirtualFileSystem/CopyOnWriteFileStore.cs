namespace NetCraft.Storage;

//CopyOnWriteFileStore 写时复制文件系统的存储信息 对应原版 net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFileStore
//空间统计取自临时目录所在的真实存储
public sealed class CopyOnWriteFileStore
{
    private readonly CopyOnWriteFileSystem _fs;

    public CopyOnWriteFileStore(string name, CopyOnWriteFileSystem fs)
    {
        Name = name;
        _fs = fs;
    }

    public string Name { get; }

    public string Type => "copy-on-write";

    public bool IsReadOnly => false;

    public long TotalSpace => Drive.TotalSize;

    public long UsableSpace => Drive.AvailableFreeSpace;

    public long UnallocatedSpace => Drive.TotalFreeSpace;

    public bool SupportsBasicAttributeView => true;

    private DriveInfo Drive => new(Path.GetPathRoot(Path.GetFullPath(_fs.TmpDirectory))!);
}
