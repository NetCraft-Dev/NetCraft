namespace NetCraft.Gpu.Font;

//AssetsFontResourceAccessor IFontResourceAccessor implementation that loads resources from the assets directory
//identifier format is namespace:path, e.g. minecraft:font/include/space.json
//Maps to the file path {assetsRoot}/{namespace}/{path}
//Path separator / is converted to Path.DirectorySeparatorChar for cross-platform use
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
