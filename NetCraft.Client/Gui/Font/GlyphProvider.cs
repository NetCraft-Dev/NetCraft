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

//IGlyphInfo glyph metrics, maps to vanilla GlyphInfo
//advance is the glyph advance width; the bold/shadow offsets are used when bolding/shadowing
public interface IGlyphInfo
{
    float Advance { get; }
    float BoldOffset => 1.0f;
    float ShadowOffset => 1.0f;

    //GetAdvance adds BoldOffset when bold, making the glyph wider
    float GetAdvance(bool bold) => Advance + (bold ? BoldOffset : 0f);

    //Simple creates simple advance-only metrics, for bitmap-less glyphs such as spaces
    static IGlyphInfo Simple(float advance) => new SimpleGlyphInfo(advance);
}

//SimpleGlyphInfo simple advance-only metrics
public sealed class SimpleGlyphInfo : IGlyphInfo
{
    public float Advance { get; }
    public SimpleGlyphInfo(float advance) => Advance = advance;
}

//IGlyphBitmap rasterized bitmap, maps to vanilla GlyphBitmap
//Vanilla uploads to the GPU directly via upload(x,y,GpuTexture); we return a pixel array from GetPixels and let the Stitcher upload
//IsColored=false returns a single-channel R8 buffer of length PixelWidth*PixelHeight
//IsColored=true returns RGBA8 of length PixelWidth*PixelHeight*4
public interface IGlyphBitmap
{
    int PixelWidth { get; }
    int PixelHeight { get; }
    float Oversample { get; }
    bool IsColored { get; }

    //BearingLeft/BearingTop defaults map to the vanilla GlyphBitmap interface defaults 0.0/7.0
    float BearingLeft => 0.0f;
    float BearingTop => IGlyphProvider.Baseline;

    //Left/Right/Top/Bottom pixel coordinates converted from the oversample-scaled occupied space
    float Left => BearingLeft;
    float Right => Left + PixelWidth / Oversample;
    float Top => IGlyphProvider.Baseline - BearingTop;
    float Bottom => Top + PixelHeight / Oversample;

    //GetPixels returns the rasterized pixel data lazily, rasterizing and caching on first call
    byte[] GetPixels();
}

//BakedGlyph baked glyph, maps to vanilla BakedGlyph
//Holds IGlyphInfo metrics + Render to submit the glyph to the render context
//Vanilla createGlyph depends on the TextRenderable/Style domain objects; NetCraft replaces them with GlyphRenderOptions in the GPU layer
public abstract class BakedGlyph
{
    public abstract IGlyphInfo Info { get; }

    //Render submits the glyph to the render context; options carry render params like x/y/color/shadowColor/bold/italic
    //maps to vanilla BakedSheetGlyph.renderChar with the full italic/bold/shadow logic
    public abstract void Render(IGuiRenderContext context, in GlyphRenderOptions options);
}

//IUnbakedGlyph unbaked glyph, maps to vanilla UnbakedGlyph
//bake yields a BakedGlyph; info returns the metrics
public interface IUnbakedGlyph
{
    IGlyphInfo Info { get; }
    BakedGlyph Bake(Stitcher stitcher);

    //Stitcher stitches IGlyphInfo+IGlyphBitmap into the atlas and returns a BakedGlyph
    public interface Stitcher
    {
        BakedGlyph Stitch(IGlyphInfo info, IGlyphBitmap bitmap);
        BakedGlyph GetMissing();
    }
}

//EmptyGlyph empty glyph, maps to vanilla EmptyGlyph
//SpaceProvider uses it for spaces: advance only, no bitmap rendering
public sealed class EmptyGlyph : IUnbakedGlyph
{
    private readonly float _advance;
    public EmptyGlyph(float advance) => _advance = advance;

    public IGlyphInfo Info => IGlyphInfo.Simple(_advance);

    //Bake returns an empty BakedGlyph, no bitmap rendering, only advance
    public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher) => new EmptyBakedGlyph(Info);

    //EmptyBakedGlyph space glyph; Render submits no render commands
    private sealed class EmptyBakedGlyph : BakedGlyph
    {
        private readonly IGlyphInfo _info;
        public EmptyBakedGlyph(IGlyphInfo info) => _info = info;
        public override IGlyphInfo Info => _info;
        public override void Render(IGuiRenderContext context, in GlyphRenderOptions options) { }
    }
}

//IGlyphProvider glyph provider, maps to vanilla GlyphProvider extends AutoCloseable
//Returns an IUnbakedGlyph per codepoint; the provider chain is searched in order for the first hit
public interface IGlyphProvider : IDisposable
{
    //Baseline baseline height, maps to vanilla GlyphProvider.BASELINE=7.0
    public const float Baseline = 7.0f;

    IReadOnlySet<int> GetSupportedGlyphs();

    //GetGlyph returns the glyph for a codepoint, or null if absent
    IUnbakedGlyph? GetGlyph(int codepoint);

    //Conditional conditional provider holding provider+filter; FontSet activates it by options
    public sealed class Conditional
    {
        public IGlyphProvider Provider { get; }
        public FontOptionFilter Filter { get; }

        public Conditional(IGlyphProvider provider, FontOptionFilter filter)
        {
            Provider = provider;
            Filter = filter;
        }
    }
}
