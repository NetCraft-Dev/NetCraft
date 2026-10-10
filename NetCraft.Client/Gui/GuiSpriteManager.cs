using System.IO;
using System.Text.Json;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Gui;

//GuiSpriteManager identifier → GuiSprite cache, maps to vanilla TextureAtlas guiSprites
//assetsRoot points to the extracted/assets directory, injected by the Game layer as AppContext.BaseDirectory/assets
//identifier format namespace:path path separator / converted to DirectorySeparatorChar
//Lazily loads the PNG via GuiResourceManager.RegisterTexture and reads the same-named .mcmeta file
//LoadSprite is protected virtual for tests to override without depending on GpuDevice
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

    //GetSprite gets a GuiSprite by identifier, lazily loading the PNG + .mcmeta if not loaded
    //Returns null when the PNG is not found; the caller handles the fallback
    //On a cache hit it returns the previous result including null results, avoiding repeated IO
    public GuiSprite? GetSprite(string identifier)
    {
        if (_cache.TryGetValue(identifier, out var cached)) return cached;
        var sprite = LoadSprite(identifier);
        _cache[identifier] = sprite;
        return sprite;
    }

    //ClearCache called by VulkanGuiApp to clear the cache on swapchain recreation
    //textureId may change, invalidating the TextureSetup held by old GuiSprites
    public void ClearCache() => _cache.Clear();

    //LoadSprite loads the PNG + .mcmeta and assembles a GuiSprite
    //identifier "minecraft:textures/gui/sprites/widget/button" →
    //  pngPath = {assetsRoot}/minecraft/textures/gui/sprites/widget/button.png
    //  mcmetaPath = {assetsRoot}/minecraft/textures/gui/sprites/widget/button.png.mcmeta
    //virtual for tests to override without real file IO and GpuDevice dependency
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
                //A parse failure falls back to the default Stretch without blocking
            }
        }

        return new GuiSprite(textureId, texture, img.Width, img.Height, scaling);
    }

    //SplitIdentifier "minecraft:textures/gui/sprites/widget/button"
    //  → ("minecraft", "textures/gui/sprites/widget/button")
    //  no colon → ("minecraft", identifier), maps to AssetsFontResourceAccessor
    private protected static (string ns, string path) SplitIdentifier(string identifier)
    {
        var idx = identifier.IndexOf(':');
        return idx > 0
            ? (identifier.Substring(0, idx), identifier.Substring(idx + 1))
            : ("minecraft", identifier);
    }
}
