namespace NetCraft.Storage;

//Node, copy-on-write file tree node, maps to vanilla net.minecraft.util.filefix.virtualfilesystem.Node
//Vanilla is sealed with only two kinds, FileNode and DirectoryNode
public abstract class Node
{
    protected Node(CopyOnWriteFSPath path) => Path = path.Normalize().ToAbsolutePath();

    public DirectoryNode? Parent { get; set; }

    public CopyOnWriteFSPath Path { get; private set; }

    //Name, the node name; the root node has none
    public string? Name => Path.GetFileName()?.ToString();

    internal void SetParent(DirectoryNode parent) => Parent = parent;

    internal void SetPath(CopyOnWriteFSPath path) => Path = path.Normalize().ToAbsolutePath();
}
