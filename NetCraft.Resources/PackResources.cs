using NetCraft.Registry;

namespace NetCraft.Resources;

//PackResources, resource pack content access, maps to vanilla net.minecraft.server.packs.PackResources
//Provides access from namespace:path to a resource stream
//Subclasses implement the concrete source (folder/zip/embedded resources)
public abstract class PackResources : IDisposable
{
    public string PackId { get; }

    protected PackResources(string packId)
    {
        PackId = packId;
    }

    //GetRootResource gets a stream for a resource at the pack root (such as pack.png)
    public abstract Stream? GetRootResource(string path);

    //GetResource gets a namespace resource stream (such as minecraft:textures/block/stone.png)
    public abstract Stream? GetResource(PackType type, Identifier location);

    //ListResources lists all resources matching a path prefix
    public abstract void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output);

    //GetNamespaces gets all namespaces contained in the resource pack
    public abstract ISet<string> GetNamespaces(PackType type);

    public virtual void Dispose() { }
}

//PackType, resource type enum, maps to vanilla PackType
//CLIENT_RESOURCES client resources (textures/sounds/models)
//SERVER_DATA server data (advancements/recipes/tags/functions)
public enum PackType
{
    ClientResources,
    ServerData
}
