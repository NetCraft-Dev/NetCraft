using System.Collections.Concurrent;

namespace NetCraft.Gpu.Font;

//GlyphFont font rendering entry point, maps to vanilla Font
//Holds the FontSet provider chain + GlyphStitcher baker + BakedGlyph cache
//Draw iterates text codepoints and calls FontSet.GetGlyph → Bake → Render
//maps to vanilla Font.drawInBatch; the shadow color is computed as RGB*0.25 keeping alpha
//The GPU layer avoids the Style domain object and wraps render params in GlyphRenderOptions; bold/italic come from the caller
//F7 simplifies Draw to plain text + shadow; bold/italic are left to the Game layer's rich-text wrapper
//Named GlyphFont to avoid clashing with the NetCraft.Gpu.Font namespace, a C# language limitation
public sealed class GlyphFont
{
    private readonly FontSet _fontSet;
    //F9 changed to an interface type so tests can inject a mock stitcher; GlyphFont only uses Bake/GetMissing and does not depend on the concrete GlyphStitcher
    private readonly IUnbakedGlyph.Stitcher _stitcher;
    private readonly ConcurrentDictionary<int, BakedGlyph?> _bakedCache = new();
    private readonly int _ascent;
    private readonly int _lineHeight;

    public int Ascent => _ascent;
    public int LineHeight => _lineHeight;

    public GlyphFont(FontSet fontSet, IUnbakedGlyph.Stitcher stitcher, int ascent, int lineHeight)
    {
        _fontSet = fontSet;
        _stitcher = stitcher;
        _ascent = ascent;
        _lineHeight = lineHeight;
    }

    //Draw renders text; x/y are top coordinates, penY = y + Ascent converts to the baseline
    //When shadow=true, a non-zero ShadowColor makes BakedGlyph.Render draw a shadow
    //color is an ARGB int, maps to vanilla GlyphInstance.color
    public void Draw(IGuiRenderContext context, string text, float x, float y, int color, bool shadow)
    {
        if (string.IsNullOrEmpty(text)) return;
        float penX = x;
        float penY = y + _ascent;
        int shadowColor = shadow ? ComputeShadowColor(color) : 0;
        foreach (var ch in text)
        {
            int codepoint = ch;
            var baked = GetBaked(codepoint);
            if (baked != null)
            {
                var options = new GlyphRenderOptions(penX, penY, color, shadowColor, false, false, 1.0f, 1.0f);
                baked.Render(context, in options);
                penX += baked.Info.Advance;
            }
            else
            {
                //Missing glyphs use GetMissing as a placeholder, maps to vanilla AllMissingGlyphProvider
                var missing = _stitcher.GetMissing();
                var options = new GlyphRenderOptions(penX, penY, color, shadowColor, false, false, 1.0f, 1.0f);
                missing.Render(context, in options);
                penX += missing.Info.Advance;
            }
        }
    }

    //MeasureText measures the text pixel width for widgets to compute alignment offsets
    public float MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        float width = 0;
        foreach (var ch in text)
        {
            var baked = GetBaked(ch);
            width += baked?.Info.Advance ?? 0;
        }
        return width;
    }

    //DrawGlyph renders a single glyph and returns the advance for the rich-text iterator to accumulate penX
    //maps to the per-glyph rendering of vanilla Font.drawInBatch, called by the Game layer's FormattedTextRenderer while iterating a FormattedCharSequence
    //When a codepoint is missing it calls GetMissing as a placeholder, SpecialGlyphs.Missing purple square
    //options are built by the caller from Style and contain x/y/color/bold/italic; the GPU layer does not depend on Style
    public float DrawGlyph(IGuiRenderContext context, int codepoint, in GlyphRenderOptions options)
    {
        var baked = GetBaked(codepoint) ?? _stitcher.GetMissing();
        baked.Render(context, in options);
        return baked.Info.Advance;
    }

    //GetBaked codepoint → BakedGlyph lazy bake cache, maps to the vanilla Font.getOrCreate glyph cache
    private BakedGlyph? GetBaked(int codepoint)
    {
        return _bakedCache.GetOrAdd(codepoint, cp =>
        {
            var unbaked = _fontSet.GetGlyph(cp);
            if (unbaked == null) return null;
            return unbaked.Bake(_stitcher);
        });
    }

    //ComputeShadowColor shadow color, maps to the shadowColor computation of vanilla Font.drawShadow
    //Vanilla multiplies the RGB of color by 0.25 keeping alpha
    private static int ComputeShadowColor(int color)
    {
        int a = (color >> 24) & 0xFF;
        int r = (int)(((color >> 16) & 0xFF) * 0.25f);
        int g = (int)(((color >> 8) & 0xFF) * 0.25f);
        int b = (int)((color & 0xFF) * 0.25f);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
