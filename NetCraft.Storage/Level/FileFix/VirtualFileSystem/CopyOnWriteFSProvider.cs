using NetCraft.Util;

namespace NetCraft.Storage;

//CopyOnWriteFSProvider, access entry point of the copy-on-write filesystem, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.CopyOnWriteFSProvider
//Vanilla extends FileSystemProvider; here only the used behaviors are kept: open stream, list directory, add/remove/move, check access
public sealed class CopyOnWriteFSProvider
{
    public const string Scheme = "x-mc-copy-on-write";

    private readonly CopyOnWriteFileSystem _fs;

    public CopyOnWriteFSProvider(CopyOnWriteFileSystem fileSystem) => _fs = fileSystem;

    public string GetScheme() => Scheme;

    //OpenChannel opens a file stream; before writing ensure it is copied into the temp directory, and when creating attach the node to the file tree
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

    //NewDirectoryStream lists the child paths of a directory
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

    //CreateDirectory creates an empty directory, throws when the name already exists
    public void CreateDirectory(CopyOnWriteFSPath dir)
    {
        var parentPath = dir.GetParent() ?? throw new CowFSFileAlreadyExistsException(dir.ToString());
        var parentFolder = _fs.FileTree.DirectoryByPath(parentPath);
        var name = dir.GetFileName()?.ToString() ?? throw new CowFSFileAlreadyExistsException(dir.ToString());
        if (parentFolder.GetChild(name) is not null) throw new CowFSFileAlreadyExistsException(dir.ToString());
        parentFolder.AddChild(new DirectoryNode(dir));
    }

    //Delete detaches a node; both the root node and a non-empty directory are rejected
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

    //Move re-attaches a node; when the target exists, replaceExisting decides overwrite or error
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

    //CheckAccess: directories check the temp directory, files check the real storage; write access requires not read-only
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

    //ReadAttributes gives a placeholder attribute set for directories and files
    public DummyFileAttributes ReadAttributes(CopyOnWriteFSPath path)
    {
        var node = _fs.FileTree.ByPath(path);
        return node is DirectoryNode ? DummyFileAttributes.Directory : DummyFileAttributes.File;
    }

    public CopyOnWriteFSPath GetRealPath(CopyOnWriteFSPath path) => _fs.FileTree.ByPath(path.ToAbsolutePath()).Path;

    //OpenStream, the real file stream: exclusive for write, shared for read
    private static Stream OpenStream(string path, bool write, bool create)
    {
        var mode = write && create ? FileMode.OpenOrCreate : FileMode.Open;
        return new FileStream(path, mode, write ? FileAccess.ReadWrite : FileAccess.Read, write ? FileShare.None : FileShare.Read);
    }
}
