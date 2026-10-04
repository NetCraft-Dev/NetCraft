namespace NetCraft.Gpu.Font;

//AssetsFontResourceAccessor IFontResourceAccessor 实现从 assets 目录加载资源
//identifier 格式 namespace:path 如 minecraft:font/include/space.json
//映射到 {assetsRoot}/{namespace}/{path} 文件路径
//路径分隔符 / 转 Path.DirectorySeparatorChar 跨平台
public sealed class AssetsFontResourceAccessor : IFontResourceAccessor
{
    private readonly string _assetsRoot;

    public AssetsFontResourceAccessor(string assetsRoot) => _assetsRoot = assetsRoot;

    public Stream? OpenResource(string identifier)
    {
        var parts = identifier.Split(':', 2);
        string ns, path;
        if (parts.Length > 1)
        {
            ns = parts[0];
            path = parts[1];
        }
        else
        {
            ns = "minecraft";
            path = identifier;
        }

        var fullPath = Path.Combine(_assetsRoot, ns, path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) return null;
        return File.OpenRead(fullPath);
    }
}
