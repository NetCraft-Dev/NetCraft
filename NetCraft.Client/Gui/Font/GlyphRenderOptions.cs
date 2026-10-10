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

//GlyphRenderOptions glyph render params, maps to vanilla BakedSheetGlyph$GlyphInstance
//Wraps x/y/color/shadowColor/bold/italic/boldOffset/shadowOffset
//Replaces the vanilla Style domain object; the GPU layer avoids rich-text semantics
//HasShadow is determined by ShadowColor!=0, maps to vanilla hasShadow=shadowColor()!=0
public readonly record struct GlyphRenderOptions(
    float X,
    float Y,
    int Color,
    int ShadowColor,
    bool Bold,
    bool Italic,
    float BoldOffset,
    float ShadowOffset)
{
    //HasShadow a non-zero shadow color means a shadow must be drawn, maps to vanilla GlyphInstance.hasShadow
    public bool HasShadow => ShadowColor != 0;

    //Simple creates plain glyph params with no shadow or styling
    public static GlyphRenderOptions Simple(float x, float y, int color)
        => new(x, y, color, 0, false, false, 1.0f, 1.0f);

    //WithShadow creates glyph params with a shadow; shadowColor is the shadow color and shadowOffset the offset
    public GlyphRenderOptions WithShadow(int shadowColor, float shadowOffset)
        => this with { ShadowColor = shadowColor, ShadowOffset = shadowOffset };
}
