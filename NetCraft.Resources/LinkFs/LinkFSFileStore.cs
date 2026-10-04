namespace NetCraft.Resources;

//LinkFSFileStore 链接文件系统的存储信息 对应原版 net.minecraft.server.packs.linkfs.LinkFSFileStore
//虚拟只读存储 不占空间 只支持 basic 属性视图
public sealed class LinkFSFileStore
{
    public LinkFSFileStore(string name) => Name = name;

    public string Name { get; }

    public string Type => "index";

    public bool IsReadOnly => true;

    public long TotalSpace => 0L;

    public long UsableSpace => 0L;

    public long UnallocatedSpace => 0L;

    public bool SupportsBasicAttributeView => true;
}
