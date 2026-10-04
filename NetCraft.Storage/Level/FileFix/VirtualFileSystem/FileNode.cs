namespace NetCraft.Storage;

//FileNode 写时复制文件树里的文件节点 对应原版 net.minecraft.util.filefix.virtualfilesystem.FileNode
//IsCopy 为真表示已复制到临时目录 写入前必须先 EnsureCopy
public sealed class FileNode : Node
{
    public FileNode(CopyOnWriteFSPath path, string storagePath, bool isCopy) : base(path)
    {
        StoragePath = storagePath;
        IsCopy = isCopy;
    }

    public string StoragePath { get; private set; }

    public bool IsCopy { get; private set; }

    //EnsureCopy 首次写入前把真实文件复制进临时目录 之后改动只落在副本上
    public void EnsureCopy()
    {
        if (IsCopy) return;
        var tempFile = Path.FileSystem.CreateTemporaryFilePath();
        File.Copy(StoragePath, tempFile, true);
        StoragePath = tempFile;
        IsCopy = true;
    }

    //DeleteCopy 删除临时副本 原始文件保持不动
    public void DeleteCopy()
    {
        if (IsCopy) File.Delete(StoragePath);
    }
}
