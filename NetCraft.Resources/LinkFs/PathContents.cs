namespace NetCraft.Resources;

//PathContents 链接文件系统里路径指向的内容 对应原版 net.minecraft.server.packs.linkfs.PathContents
//Missing 表示路径不存在 Relative 表示尚未落到文件树上的相对路径
public abstract record PathContents
{
    public static readonly PathContents Missing = new MissingContents();
    public static readonly PathContents Relative = new RelativeContents();

    //IsRelative 是否相对路径 只有相对路径才不是绝对
    public bool IsRelative => ReferenceEquals(this, Relative);

    //IsRelativeOrMissing 相对或不存在 这两种都没有真实内容
    public bool IsRelativeOrMissing => ReferenceEquals(this, Relative) || ReferenceEquals(this, Missing);

    //DirectoryContents 目录内容 持有子路径表
    public sealed record DirectoryContents(Dictionary<string, LinkFSPath> Children) : PathContents;

    //FileContents 文件内容 指向真实文件路径
    public sealed record FileContents(string Target) : PathContents;

    private sealed record MissingContents : PathContents;

    private sealed record RelativeContents : PathContents;
}
