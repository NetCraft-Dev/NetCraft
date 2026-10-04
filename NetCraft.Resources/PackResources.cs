using NetCraft.Registry;

namespace NetCraft.Resources;

//PackResources 资源包内容访问对应原版 net.minecraft.server.packs.PackResources
//提供 namespace:path 到资源流的访问
//子类实现具体来源（文件夹/zip/嵌入资源）
public abstract class PackResources : IDisposable
{
    public string PackId { get; }

    protected PackResources(string packId)
    {
        PackId = packId;
    }

    //GetRootResource 获取资源包根目录下的资源流（如 pack.png）
    public abstract Stream? GetRootResource(string path);

    //GetResource 获取命名空间资源流（如 minecraft:textures/block/stone.png）
    public abstract Stream? GetResource(PackType type, Identifier location);

    //ListResources 列出指定路径前缀下所有匹配资源
    public abstract void ListResources(PackType type, string namespaceName, string pathPrefix, ISet<Identifier> output);

    //GetNamespaces 获取资源包包含的所有命名空间
    public abstract ISet<string> GetNamespaces(PackType type);

    public virtual void Dispose() { }
}

//PackType 资源类型枚举对应原版 PackType
//CLIENT_RESOURCES 客户端资源（贴图/音效/model）
//SERVER_DATA 服务端数据（advancements/recipes/tags/functions）
public enum PackType
{
    ClientResources,
    ServerData
}
