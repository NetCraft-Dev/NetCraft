namespace NetCraft.Resources;

//LinkFSFileStore, storage info for the link file system, maps to vanilla net.minecraft.server.packs.linkfs.LinkFSFileStore
//Virtual readonly storage, consumes no space, only supports the basic attribute view
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
