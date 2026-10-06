namespace NetCraft.Storage;

//FileNode, file node in the copy-on-write file tree, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.FileNode
//IsCopy true means it was copied into the temp directory; EnsureCopy must run before writing
public sealed class FileNode : Node
{
    public FileNode(CopyOnWriteFSPath path, string storagePath, bool isCopy) : base(path)
    {
        StoragePath = storagePath;
        IsCopy = isCopy;
    }

    public string StoragePath { get; private set; }

    public bool IsCopy { get; private set; }

    //EnsureCopy copies the real file into the temp directory before the first write; later changes land only on the copy
    public void EnsureCopy()
    {
        if (IsCopy) return;
        var tempFile = Path.FileSystem.CreateTemporaryFilePath();
        File.Copy(StoragePath, tempFile, true);
        StoragePath = tempFile;
        IsCopy = true;
    }

    //DeleteCopy deletes the temp copy, leaving the original file untouched
    public void DeleteCopy()
    {
        if (IsCopy) File.Delete(StoragePath);
    }
}
