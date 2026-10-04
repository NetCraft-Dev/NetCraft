using NetCraft.Util;

namespace NetCraft.Storage;

//CopyOnWriteFSProvider 写时复制文件系统的访问入口 对应原版 net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFSProvider
//原版继承 FileSystemProvider 这里只保留开流 列目录 增删移 查访问这些会被用到的行为
public sealed class CopyOnWriteFSProvider
{
    public const string Scheme = "x-mc-copy-on-write";

    private readonly CopyOnWriteFileSystem _fs;

    public CopyOnWriteFSProvider(CopyOnWriteFileSystem fileSystem) => _fs = fileSystem;

    public string GetScheme() => Scheme;

    //OpenChannel 打开文件流 写前先确保复制进临时目录 新建时把节点挂进文件树
    public Stream OpenChannel(CopyOnWriteFSPath path, bool write, bool create)
    {
        var node = _fs.FileTree.ByPathOrNull(path);
        if (node is FileNode file)
        {
            if (write) file.EnsureCopy();
            return OpenStream(file.StoragePath, write, create);
        }
        if (node is DirectoryNode) throw new CowFSFileSystemException($"{path}: not a regular file");
        if (!create) throw new CowFSNoSuchFileException(path.ToString());
        var parentPath = path.GetParent() ?? throw new CowFSNoSuchFileException(path.ToString());
        var parent = _fs.FileTree.DirectoryByPath(parentPath);
        var tempFile = _fs.CreateTemporaryFilePath();
        var result = OpenStream(tempFile, write, create);
        parent.AddChild(new FileNode(path, tempFile, true));
        return result;
    }

    //NewDirectoryStream 列出目录下的子路径
    public IReadOnlyList<CopyOnWriteFSPath> NewDirectoryStream(CopyOnWriteFSPath dir, Func<CopyOnWriteFSPath, bool>? filter = null)
    {
        var directoryNode = _fs.FileTree.DirectoryByPath(dir);
        var result = new List<CopyOnWriteFSPath>();
        foreach (var child in directoryNode.Children)
        {
            if (filter is null || filter(child.Path)) result.Add(child.Path);
        }
        return result;
    }

    //CreateDirectory 建空目录 同名已存在抛异常
    public void CreateDirectory(CopyOnWriteFSPath dir)
    {
        var parentPath = dir.GetParent() ?? throw new CowFSFileAlreadyExistsException(dir.ToString());
        var parentFolder = _fs.FileTree.DirectoryByPath(parentPath);
        var name = dir.GetFileName()?.ToString() ?? throw new CowFSFileAlreadyExistsException(dir.ToString());
        if (parentFolder.GetChild(name) is not null) throw new CowFSFileAlreadyExistsException(dir.ToString());
        parentFolder.AddChild(new DirectoryNode(dir));
    }

    //Delete 摘掉节点 根节点与非空目录都拒绝
    public void Delete(CopyOnWriteFSPath path)
    {
        var node = _fs.FileTree.ByPath(path);
        if (node.Parent is null) throw new CowFSFileSystemException("Can't remove root");
        var name = node.Name ?? throw new CowFSFileSystemException("Node is missing a name");
        if (node is DirectoryNode directory)
        {
            if (directory.Children.Count > 0) throw new CowFSDirectoryNotEmptyException(path.ToString());
        }
        else if (node is FileNode file)
        {
            file.DeleteCopy();
        }
        node.Parent.RemoveChild(name);
    }

    //Move 改挂节点 目标已存在时按 replaceExisting 决定覆盖还是报错
    public void Move(CopyOnWriteFSPath source, CopyOnWriteFSPath target, bool replaceExisting)
    {
        if (source.IsRoot()) throw new CowFSFileSystemException($"{source}: can't move root directory");
        var sourceNode = _fs.FileTree.ByPathOrNull(source) ?? throw new CowFSNoSuchFileException(source.ToString());
        var parentPath = target.ToAbsolutePath().GetParent() ?? throw new CowFSFileAlreadyExistsException(target.ToString());
        if (_fs.FileTree.ByPathOrNull(parentPath) is not DirectoryNode targetParent) throw new CowFSNoSuchFileException(target.ToString());
        var newName = target.GetFileName()?.ToString() ?? throw new CowFSNoSuchFileException(target.ToString());
        var oldChild = targetParent.GetChild(newName);
        if (oldChild is not null)
        {
            if (ReferenceEquals(oldChild, sourceNode)) return;
            if (replaceExisting) targetParent.RemoveChild(newName);
            else throw new CowFSFileAlreadyExistsException(target.ToString());
        }
        sourceNode.Parent?.RemoveChild(sourceNode.Name ?? string.Empty);
        sourceNode.SetPath(target);
        targetParent.AddChild(sourceNode);
    }

    //CheckAccess 目录看临时目录 文件看真实存储 写访问要求不作只读
    public void CheckAccess(CopyOnWriteFSPath path, bool read)
    {
        var node = _fs.FileTree.ByPath(path);
        var target = node is DirectoryNode ? _fs.TmpDirectory : ((FileNode)node).StoragePath;
        if (!read)
        {
            if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0) throw new UnauthorizedAccessException(target);
            return;
        }
        if (!File.Exists(target) && !Directory.Exists(target)) throw new FileNotFoundException(target);
    }

    //ReadAttributes 目录与文件各给一份占位属性
    public DummyFileAttributes ReadAttributes(CopyOnWriteFSPath path)
    {
        var node = _fs.FileTree.ByPath(path);
        return node is DirectoryNode ? DummyFileAttributes.Directory : DummyFileAttributes.File;
    }

    public CopyOnWriteFSPath GetRealPath(CopyOnWriteFSPath path) => _fs.FileTree.ByPath(path.ToAbsolutePath()).Path;

    //OpenStream 真实文件流 写独占 读共享
    private static Stream OpenStream(string path, bool write, bool create)
    {
        var mode = write && create ? FileMode.OpenOrCreate : FileMode.Open;
        return new FileStream(path, mode, write ? FileAccess.ReadWrite : FileAccess.Read, write ? FileShare.None : FileShare.Read);
    }
}
