namespace NetCraft.Resources;

//PathContents, the content a path points to in the link file system, maps to vanilla net.minecraft.server.packs.linkfs.PathContents
//Missing means the path does not exist, Relative means a relative path not yet resolved onto the file tree
public abstract record PathContents
{
    public static readonly PathContents Missing = new MissingContents();
    public static readonly PathContents Relative = new RelativeContents();

    //IsRelative whether this is a relative path, only a relative path is not absolute
    public bool IsRelative => ReferenceEquals(this, Relative);

    //IsRelativeOrMissing relative or missing, neither has real content
    public bool IsRelativeOrMissing => ReferenceEquals(this, Relative) || ReferenceEquals(this, Missing);

    //DirectoryContents, directory content, holds the child path map
    public sealed record DirectoryContents(Dictionary<string, LinkFSPath> Children) : PathContents;

    //FileContents, file content, points to the real file path
    public sealed record FileContents(string Target) : PathContents;

    private sealed record MissingContents : PathContents;

    private sealed record RelativeContents : PathContents;
}
