using System.IO;
using System.Text.Json;

namespace NetCraft.Gpu.Sprite;

//GuiSpriteManager identifier → GuiSprite 缓存对标原版 TextureAtlas guiSprites
//assetsRoot 指向 extracted/assets 目录由 Game 层注入 AppContext.BaseDirectory/assets
//identifier 格式 namespace:path 路径分隔符 / 转 DirectorySeparatorChar
//懒加载 PNG 调 GuiResourceManager.RegisterTexture 读同名 .mcmeta 文件
//LoadSprite 为 protected virtual 供测试覆盖避免依赖 GpuDevice
public class GuiSpriteManager
{
    private readonly string _assetsRoot;
    private readonly GuiResourceManager _resourceManager;
    private readonly Dictionary<string, GuiSprite?> _cache = new();

    public GuiSpriteManager(string assetsRoot, GuiResourceManager resourceManager)
    {
        _assetsRoot = assetsRoot;
        _resourceManager = resourceManager;
    }

    //GetSprite 按 identifier 取 GuiSprite 未加载则懒加载 PNG + .mcmeta
    //找不到 PNG 返回 null 调用方自行处理 fallback
    //缓存命中直接返回上一次结果包含 null 结果避免重复 IO
    public GuiSprite? GetSprite(string identifier)
    {
        if (_cache.TryGetValue(identifier, out var cached)) return cached;
        var sprite = LoadSprite(identifier);
        _cache[identifier] = sprite;
        return sprite;
    }

    //ClearCache swapchain 重建时由 VulkanGuiApp 调清空缓存
    //textureId 可能变化旧 GuiSprite 持有的 TextureSetup 失效
    public void ClearCache() => _cache.Clear();

    //LoadSprite 加载 PNG + .mcmeta 组装 GuiSprite
    //identifier "minecraft:textures/gui/sprites/widget/button" →
    //  pngPath = {assetsRoot}/minecraft/textures/gui/sprites/widget/button.png
    //  mcmetaPath = {assetsRoot}/minecraft/textures/gui/sprites/widget/button.png.mcmeta
    //virtual 供测试覆盖避免真实文件 IO 和 GpuDevice 依赖
    protected virtual GuiSprite? LoadSprite(string identifier)
    {
        var (ns, path) = SplitIdentifier(identifier);
        var relativePath = path.Replace('/', Path.DirectorySeparatorChar);
        var pngPath = Path.Combine(_assetsRoot, ns, relativePath + ".png");
        var mcmetaPath = pngPath + ".mcmeta";

        if (!File.Exists(pngPath)) return null;

        int textureId = _resourceManager.RegisterTexture(pngPath);
        if (textureId == 0) return null;
        var texture = _resourceManager.ResolveTexture(textureId);
        if (texture is null || texture.Texture0 is null) return null;

        var img = texture.Texture0;
        var scaling = GuiSpriteScaling.Default;
        if (File.Exists(mcmetaPath))
        {
            try
            {
                var json = File.ReadAllBytes(mcmetaPath);
                using var doc = JsonDocument.Parse(json);
                scaling = GuiMetadataSection.Parse(doc.RootElement.Clone());
            }
            catch
            {
                //解析失败用默认 Stretch 渲染不阻断
            }
        }

        return new GuiSprite(textureId, texture, img.Width, img.Height, scaling);
    }

    //SplitIdentifier "minecraft:textures/gui/sprites/widget/button"
    //  → ("minecraft", "textures/gui/sprites/widget/button")
    //  无冒号 → ("minecraft", identifier) 对标 AssetsFontResourceAccessor
    private protected static (string ns, string path) SplitIdentifier(string identifier)
    {
        var idx = identifier.IndexOf(':');
        return idx > 0
            ? (identifier.Substring(0, idx), identifier.Substring(idx + 1))
            : ("minecraft", identifier);
    }
}
