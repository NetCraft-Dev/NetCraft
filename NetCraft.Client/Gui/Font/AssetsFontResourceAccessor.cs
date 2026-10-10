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
namespace NetCraft.Client.Gui.Font;

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
