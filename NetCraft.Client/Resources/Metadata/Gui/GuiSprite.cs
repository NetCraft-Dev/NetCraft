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
namespace NetCraft.Client.Resources.Metadata.Gui;

//GuiSprite a single sprite corresponding to one PNG + .mcmeta scaling config
//A simplified TextureAtlasSprite; NetCraft does no atlas packing, each sprite gets its own GpuImage
//TextureId returned by GuiResourceManager.RegisterTexture for GuiRenderContext.DrawImage
//Texture holds the GpuImage+Sampler registered by GuiResourceManager
//Width/Height are the source PNG's actual pixel size Scaling is the parsed .mcmeta result
public sealed class GuiSprite
{
    public int TextureId { get; }
    public TextureSetup Texture { get; }
    public int Width { get; }
    public int Height { get; }
    public GuiSpriteScaling Scaling { get; }

    public GuiSprite(int textureId, TextureSetup texture, int width, int height, GuiSpriteScaling scaling)
    {
        TextureId = textureId;
        Texture = texture;
        Width = width;
        Height = height;
        Scaling = scaling;
    }
}
