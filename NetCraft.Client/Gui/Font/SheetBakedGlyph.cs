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

//SheetBakedGlyph glyph baked into an atlas, maps to vanilla BakedSheetGlyph
//Holds IGlyphInfo metrics + UV coordinates + Left/Right/Top/Bottom pixel offsets + TextureSetup + GlyphRenderTypes
//Render fully implements the italic/bold/shadow logic, maps to vanilla BakedSheetGlyph.renderChar
//italic uses the shearTop/shearBottom formula 1.0-0.25*up/down; bold uses extraThickness=0.1 with a second thickened draw
//shadow uses the PolygonOffset pipeline offset by shadowOffset to draw the shadow color
internal sealed class SheetBakedGlyph : BakedGlyph
{
    public override IGlyphInfo Info { get; }

    //UVs are inset by 0.01 pixels to avoid sampling out of bounds
    internal readonly float U0;
    internal readonly float V0;
    internal readonly float U1;
    internal readonly float V1;

    //Left/Right/Top/Bottom glyph pixel offsets relative to the baseline, maps to vanilla BakedSheetGlyph left/right/up/down
    //Left/Top are the top-left offsets, Right/Bottom the bottom-right; combined with x/y at render time to get the four corners
    internal readonly float Left;
    internal readonly float Right;
    internal readonly float Top;
    internal readonly float Bottom;

    //TextureSetup binds the glyph atlas texture and is passed to IGuiRenderContext.DrawGlyphQuad on Render
    private readonly TextureSetup _textureSetup;
    //GlyphRenderTypes the three pipelines normal/seeThrough/polygonOffset, selected by DisplayMode on Render
    private readonly GlyphRenderTypes _renderTypes;

    public SheetBakedGlyph(IGlyphInfo info, float u0, float v0, float u1, float v1,
        float left, float right, float top, float bottom,
        TextureSetup textureSetup, GlyphRenderTypes renderTypes)
    {
        Info = info;
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
        _textureSetup = textureSetup;
        _renderTypes = renderTypes;
    }

    //Render full render logic, maps to vanilla BakedSheetGlyph.renderChar
    //The shadow uses the PolygonOffset pipeline offset by shadowOffset to draw the shadow color; bold also thickens the shadow
    //The main glyph uses the Normal pipeline; when bold a second draw offset by boldOffset thickens it
    public override void Render(IGuiRenderContext context, in GlyphRenderOptions options)
    {
        float x = options.X;
        float y = options.Y;
        bool bold = options.Bold;
        bool italic = options.Italic;

        //The shadow is drawn first, maps to vanilla renderChar: with hasShadow, polygonOffset shadow before the normal main glyph
        if (options.HasShadow)
        {
            RenderSingle(context, x + options.ShadowOffset, y + options.ShadowOffset,
                options.ShadowColor, bold, italic, DisplayMode.PolygonOffset);
            if (bold)
            {
                RenderSingle(context, x + options.BoldOffset + options.ShadowOffset, y + options.ShadowOffset,
                    options.ShadowColor, true, italic, DisplayMode.PolygonOffset);
            }
        }

        //Main glyph
        RenderSingle(context, x, y, options.Color, bold, italic, DisplayMode.Normal);
        if (bold)
        {
            RenderSingle(context, x + options.BoldOffset, y, options.Color, true, italic, DisplayMode.Normal);
        }
    }

    //RenderSingle submits a single glyph quad to the render context
    //italic shearTop/shearBottom formula 1.0-0.25*Top/Bottom; bold extraThickness=0.1
    //4 vertices, maps to vanilla top-left→bottom-left→bottom-right→top-right
    private void RenderSingle(IGuiRenderContext context, float x, float y, int color,
        bool bold, bool italic, DisplayMode mode)
    {
        //italic shear, maps to vanilla 1.0 - 0.25 * up/down
        float shearTop = italic ? 1.0f - 0.25f * Top : 0.0f;
        float shearBottom = italic ? 1.0f - 0.25f * Bottom : 0.0f;
        //bold extraThickness, maps to the vanilla 0.1f when bold
        float extra = bold ? 0.1f : 0.0f;

        //4 vertices, maps to vanilla BakedSheetGlyph.renderChar
        float x0 = x + Left + shearTop - extra;
        float y0 = y + Top - extra;
        float x1 = x + Left + shearBottom - extra;
        float y1 = y + Bottom + extra;
        float x2 = x + Right + shearBottom + extra;
        float y2 = y + Bottom + extra;
        float x3 = x + Right + shearTop + extra;
        float y3 = y + Top - extra;

        var pipeline = _renderTypes.Select(mode);
        context.DrawGlyphQuad(pipeline, _textureSetup,
            x0, y0, x1, y1, x2, y2, x3, y3,
            U0, V0, U1, V1, color);
    }
}
