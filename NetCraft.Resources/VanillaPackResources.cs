using NetCraft.Registry;

namespace NetCraft.Resources;

//VanillaPackResources 原版内置资源包对应原版 net.minecraft.server.packs.VanillaPackResources
//原版从jar内assets/data目录加载C#简化为从指定根目录读取默认资源
//PackId固定vanilla表示原版内置资源优先级最低被其他资源包覆盖
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
