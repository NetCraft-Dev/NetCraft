namespace NetCraft.Storage;

//CopyOnWriteFileStore, file store info for the copy-on-write filesystem, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFileStore
//Space stats come from the real store holding the temp directory
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
