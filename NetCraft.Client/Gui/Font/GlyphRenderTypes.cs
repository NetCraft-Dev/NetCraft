using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
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

//DisplayMode glyph render mode, maps to vanilla Font.DisplayMode
//NORMAL plain text SEE_THROUGH see-through rendering POLYGON_OFFSET polygon offset used for shadows
public enum DisplayMode
{
    Normal,
    SeeThrough,
    PolygonOffset
}

//GlyphRenderTypes glyph render pipeline set, maps to vanilla GlyphRenderTypes
//Holds the three RenderPipelines normal/seeThrough/polygonOffset + guiPipeline
//createForGrayscaleTexture returns the grayscale pipeline set for R8 atlases
//createForColorTexture returns the color pipeline set for RGBA8 atlases
//select(DisplayMode) returns the matching pipeline for a DisplayMode
public sealed class GlyphRenderTypes
{
    public RenderPipeline Normal { get; }
    public RenderPipeline SeeThrough { get; }
    public RenderPipeline PolygonOffset { get; }
    public RenderPipeline GuiPipeline { get; }

    public GlyphRenderTypes(RenderPipeline normal, RenderPipeline seeThrough, RenderPipeline polygonOffset, RenderPipeline guiPipeline)
    {
        Normal = normal;
        SeeThrough = seeThrough;
        PolygonOffset = polygonOffset;
        GuiPipeline = guiPipeline;
    }

    //createForGrayscaleTexture grayscale atlases use the GUI_TEXT_GRAYSCALE pipeline series
    public static GlyphRenderTypes CreateForGrayscaleTexture()
        => new(RenderPipelines.GUI_TEXT_GRAYSCALE,
            RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH,
            RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET,
            RenderPipelines.GUI_TEXT_GRAYSCALE);

    //createForColorTexture color atlases use the GUI_TEXT pipeline series
    public static GlyphRenderTypes CreateForColorTexture()
        => new(RenderPipelines.GUI_TEXT,
            RenderPipelines.GUI_TEXT_SEE_THROUGH,
            RenderPipelines.GUI_TEXT_POLYGON_OFFSET,
            RenderPipelines.GUI_TEXT);

    //select returns the matching pipeline for a DisplayMode, maps to vanilla GlyphRenderTypes.select
    public RenderPipeline Select(DisplayMode mode) => mode switch
    {
        DisplayMode.Normal => Normal,
        DisplayMode.SeeThrough => SeeThrough,
        DisplayMode.PolygonOffset => PolygonOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
