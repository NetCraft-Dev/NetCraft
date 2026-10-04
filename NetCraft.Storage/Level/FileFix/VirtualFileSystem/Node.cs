namespace NetCraft.Storage;

//Node 写时复制文件树节点 对应原版 net.minecraft.util.filefix.virtualfilesystem.Node
//原版是 sealed 只许 FileNode 与 DirectoryNode 两种
public abstract class Node
{
    protected Node(CopyOnWriteFSPath path) => Path = path.Normalize().ToAbsolutePath();

    public DirectoryNode? Parent { get; set; }

    public CopyOnWriteFSPath Path { get; private set; }

    //Name 节点名 根节点没有名字
    public string? Name => Path.GetFileName()?.ToString();

    internal void SetParent(DirectoryNode parent) => Parent = parent;

    internal void SetPath(CopyOnWriteFSPath path) => Path = path.Normalize().ToAbsolutePath();
}
