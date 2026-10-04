namespace NetCraft.Storage;

//DirectoryNode 写时复制文件树里的目录节点 对应原版 net.minecraft.util.filefix.virtualfilesystem.DirectoryNode
public sealed class DirectoryNode : Node
{
    private readonly Dictionary<string, Node> _childNodes = new();

    public DirectoryNode(CopyOnWriteFSPath path) : base(path)
    {
    }

    public IReadOnlyCollection<Node> Children => _childNodes.Values;

    public void AddChild(Node child)
    {
        var name = child.Name ?? throw new ArgumentException("Child node is missing a name");
        _childNodes[name] = child;
        child.SetParent(this);
    }

    public void RemoveChild(string name) => _childNodes.Remove(name);

    public Node? GetChild(string name) => _childNodes.GetValueOrDefault(name);

    //DirectoryByPath 按路径取目录 命中文件时抛异常
    public DirectoryNode DirectoryByPath(CopyOnWriteFSPath path)
    {
        var node = ByPath(path);
        if (node is DirectoryNode directory) return directory;
        throw new CowFSNotDirectoryException($"{path} was a file, expected directory");
    }

    //FileByPath 按路径取文件 非文件一律当不存在
    public FileNode FileByPath(CopyOnWriteFSPath path)
    {
        var node = ByPathOrNull(path);
        if (node is FileNode file) return file;
        throw new CowFSNoSuchFileException(path.ToString());
    }

    public Node ByPath(CopyOnWriteFSPath path)
    {
        var node = ByPathOrNull(path);
        if (node is not null) return node;
        throw new CowFSNoSuchFileException(path.ToString());
    }

    //ByPathOrNull 逐段下钻 途中遇到文件且还没走完就当作不存在
    public Node? ByPathOrNull(CopyOnWriteFSPath path)
    {
        var nameCount = path.GetNameCount();
        var directory = this;
        for (var i = 0; i < nameCount; i++)
        {
            var name = path.GetName(i).ToString();
            if (name == ".") continue;
            if (name == "..")
            {
                if (directory.Parent is not null) directory = directory.Parent;
                continue;
            }
            var nextNode = directory.GetChild(name);
            if (nextNode is DirectoryNode nextDirectory)
            {
                directory = nextDirectory;
                continue;
            }
            return i == nameCount - 1 ? nextNode : null;
        }
        return directory;
    }
}
