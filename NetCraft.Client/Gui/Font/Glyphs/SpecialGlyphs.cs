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
namespace NetCraft.Client.Gui.Font.Glyphs;

//SpecialGlyphs procedurally generated special glyphs, maps to vanilla net.minecraft.client.gui.font.glyphs.SpecialGlyphs
//White 5x8 all white Missing 5x8 white border used as a placeholder for missing codepoints
//Pixels are generated in the static constructor; advance=width+1, maps to vanilla getAdvance returning image.width+1
//At render time the caller's color tints it; the purple square in game is a tinting effect, not the pixels themselves
public static class SpecialGlyphs
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 8;

    //Missing missing-glyph placeholder: opaque border, transparent interior, maps to vanilla SpecialGlyphs.MISSING
    //edge? -1:0 means border 0xFFFFFFFF and interior 0x00000000
    public static IUnbakedGlyph Missing { get; } = new SpecialUnbakedGlyph(BuildBitmap(IsEdge));

    //White all-white fill, maps to vanilla SpecialGlyphs.WHITE with all pixels 0xFFFFFFFF
    public static IUnbakedGlyph White { get; } = new SpecialUnbakedGlyph(BuildBitmap((x, y) => true));

    //IsEdge checks whether a pixel is on the border, maps to vanilla edge = x==0 || x+1==width || y==0 || y+1==height
    private static bool IsEdge(int x, int y)
        => x == 0 || x + 1 == GlyphWidth || y == 0 || y + 1 == GlyphHeight;

    //BuildBitmap procedurally generates the RGBA8 pixel array; edge pixels are all 0xFF, otherwise all 0x00
    //Length=width*height*4, maps to vanilla NativeImage RGBA format setPixel
    private static SpecialGlyphBitmap BuildBitmap(Func<int, int, bool> pixelProvider)
    {
        var pixels = new byte[GlyphWidth * GlyphHeight * 4];
        for (int y = 0; y < GlyphHeight; y++)
        {
            for (int x = 0; x < GlyphWidth; x++)
            {
                int idx = (y * GlyphWidth + x) * 4;
                byte v = (byte)(pixelProvider(x, y) ? 0xFF : 0x00);
                pixels[idx] = v;
                pixels[idx + 1] = v;
                pixels[idx + 2] = v;
                pixels[idx + 3] = v;
            }
        }
        return new SpecialGlyphBitmap(GlyphWidth, GlyphHeight, pixels);
    }

    //SpecialUnbakedGlyph special glyph IUnbakedGlyph implementation
    //Bake calls stitcher.Stitch to stitch into an RGBA8 atlas and returns a SheetBakedGlyph, reusing the F7 render logic
    //Info advance=width+1=6; BoldOffset/ShadowOffset use the IGlyphInfo default 1.0
    private sealed class SpecialUnbakedGlyph : IUnbakedGlyph
    {
        private readonly SpecialGlyphBitmap _bitmap;

        public SpecialUnbakedGlyph(SpecialGlyphBitmap bitmap) => _bitmap = bitmap;

        public IGlyphInfo Info => new SpecialGlyphInfo(GlyphWidth + 1);

        public BakedGlyph Bake(IUnbakedGlyph.Stitcher stitcher)
            => stitcher.Stitch(Info, _bitmap);
    }

    //SpecialGlyphInfo advance-only metrics, maps to vanilla SpecialGlyphs.getAdvance = image.width+1
    private sealed class SpecialGlyphInfo : IGlyphInfo
    {
        public float Advance { get; }
        public SpecialGlyphInfo(float advance) => Advance = advance;
    }

    //SpecialGlyphBitmap fixed RGBA8 bitmap implementing IGlyphBitmap
    //Oversample=1.0 IsColored=true; pixels come from outside and are already generated at construction
    //BearingLeft/BearingTop use the defaults 0.0/7.0(Baseline); Top=0 Bottom=8, maps to the vanilla 5x8 image position
    private sealed class SpecialGlyphBitmap : IGlyphBitmap
    {
        private readonly byte[] _pixels;

        public SpecialGlyphBitmap(int width, int height, byte[] pixels)
        {
            PixelWidth = width;
            PixelHeight = height;
            _pixels = pixels;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public float Oversample => 1.0f;
        public bool IsColored => true;
        public byte[] GetPixels() => _pixels;
    }
}
