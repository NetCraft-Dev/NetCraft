using NetCraft.Registry;

namespace NetCraft.Resources;

//VanillaPackResources, the built-in vanilla resource pack, maps to vanilla net.minecraft.server.packs.VanillaPackResources
//Vanilla loads from the assets/data directories inside the jar, C# simplifies this to reading default resources from a given root directory
//PackId is fixed to vanilla, the built-in pack has the lowest priority and is overridden by other resource packs
public sealed class VanillaPackResources : PackResources
{
    private readonly FolderPackResources _delegate;

    public VanillaPackResources(string rootPath) : base("vanilla")
    {
        _delegate = new FolderPackResources("vanilla", rootPath);
    }

    public override Stream? GetRootResource(string path) => _delegate.GetRootResource(path);

    public override Stream? GetResource(PackType type, Identifier location) => _delegate.GetResource(type, location);

    public override void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output)
        => _delegate.ListResources(type, namespaceName, pathPrefix, output);

    public override ISet<string> GetNamespaces(PackType type) => _delegate.GetNamespaces(type);
}
