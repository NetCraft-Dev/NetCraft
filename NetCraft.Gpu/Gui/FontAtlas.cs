using StbTrueTypeSharp;

namespace NetCraft.Gpu;

//GlyphInfo position and metrics of a single glyph in the atlas
//U0/V0 are the glyph's top-left UV in the atlas, U1/V1 the bottom-right UV
//Advance is the character advance width, OffsetX/OffsetY the glyph offset relative to the baseline
//F7 marks this Obsolete; replaced by the Font/GlyphStitcher/SheetBakedGlyph dynamic bake path, kept as a fallback
[System.Obsolete("F7 replaced by the Font dynamic bake path; GuiRenderContext no longer uses FontAtlas when Font is non-null")]
public readonly struct GlyphInfo
{
    public readonly int X;
    public readonly int Y;
    public readonly int Width;
    public readonly int Height;
    public readonly float OffsetX;
    public readonly float OffsetY;
    public readonly float Advance;
    public readonly float U0;
    public readonly float V0;
    public readonly float U1;
    public readonly float V1;

    public GlyphInfo(int x, int y, int w, int h, float ox, float oy, float adv, float u0, float v0, float u1, float v1)
    {
        X = x;
        Y = y;
        Width = w;
        Height = h;
        OffsetX = ox;
        OffsetY = oy;
        Advance = adv;
        U0 = u0;
        V0 = v0;
        U1 = u1;
        V1 = v1;
    }
}

//FontAtlas glyph atlas
//Loads a TTF with StbTrueType and rasterizes ASCII 32-126 + CJK U+4E00..U+9FFF into an R8 single-channel texture
//The CJK range is large; glyphs that do not fit are skipped and lookups fall back to a question mark
//Supports multi-resolution re-rasterization for DPI adaptation
//F7 marks this Obsolete; replaced by the Font dynamic bake path, kept as a system-font fallback
[System.Obsolete("F7 replaced by the Font/GlyphStitcher/SheetBakedGlyph dynamic bake path, kept as a system-font fallback")]
public sealed unsafe class FontAtlas : IDisposable
{
    private const int FirstAsciiChar = 32;
    private const int AsciiCount = 95;
    //CJK unified ideographs U+4E00..U+9FFF covering common Han characters
    private const int FirstCjkChar = 0x4E00;
    private const int CjkCount = 0x9FFF - 0x4E00 + 1;
    private const float DefaultFontSize = 18f;

    private readonly byte[] _ttfBytes;
    private readonly StbTrueType.stbtt_fontinfo _fontInfo;
    private readonly float _fontSize;
    private readonly int _ascent;
    private readonly int _descent;
    private readonly int _lineGap;

    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    public byte[] AtlasPixels { get; }
    public int LineHeight { get; }
    //Ascent distance from the baseline to the glyph top; DrawText converts to baseline with this rather than LineHeight
    //LineHeight = ascent - descent, which overshoots ascent by -descent and pushes the baseline down, most visible with CJK
    public int Ascent => _ascent;
    public float FontSize => _fontSize;
    public IReadOnlyDictionary<int, GlyphInfo> Glyphs { get; }

    //FromSystemFont finds and loads the first available TTF among the common system font paths
    //Returns null when none is found; the caller decides the fallback
    public static FontAtlas? FromSystemFont(float fontSize = DefaultFontSize)
    {
        foreach (var path in CandidateFontPaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                var bytes = File.ReadAllBytes(path);
                return new FontAtlas(bytes, fontSize);
            }
            catch
            {
                //Font load failed at this path, try the next
            }
        }
        return null;
    }

    //FromBytes builds the atlas from a TTF byte sequence
    public FontAtlas(byte[] ttfBytes, float fontSize = DefaultFontSize)
    {
        _ttfBytes = ttfBytes;
        _fontSize = fontSize;
        _fontInfo = new StbTrueType.stbtt_fontinfo();
        fixed (byte* data = ttfBytes)
        {
            if (StbTrueType.stbtt_InitFont(_fontInfo, data, 0) == 0)
                throw new InvalidOperationException("stbtt_InitFont failed");
        }

        int ascent, descent, lineGap;
        StbTrueType.stbtt_GetFontVMetrics(_fontInfo, &ascent, &descent, &lineGap);
        _ascent = ascent;
        _descent = descent;
        _lineGap = lineGap;
        LineHeight = _ascent - _descent;

        //The atlas is enlarged to 2048x2048 to hold common CJK Han characters
        AtlasWidth = 2048;
        AtlasHeight = 2048;
        AtlasPixels = new byte[AtlasWidth * AtlasHeight];

        var asciiPacked = new StbTrueType.stbtt_packedchar[AsciiCount];
        var cjkPacked = new StbTrueType.stbtt_packedchar[CjkCount];
        var packContext = new StbTrueType.stbtt_pack_context();
        fixed (byte* pixels = AtlasPixels)
        {
            if (StbTrueType.stbtt_PackBegin(packContext, pixels, AtlasWidth, AtlasHeight, AtlasWidth, 1, null) == 0)
                throw new InvalidOperationException("stbtt_PackBegin failed");

            fixed (byte* fontData = ttfBytes)
            {
                fixed (StbTrueType.stbtt_packedchar* ap = asciiPacked)
                {
                    if (StbTrueType.stbtt_PackFontRange(packContext, fontData, 0, fontSize,
                            FirstAsciiChar, AsciiCount, ap) == 0)
                        throw new InvalidOperationException("stbtt_PackFontRange ASCII failed");
                }
                //The CJK range is large so partial failure is allowed; glyphs that do not fit are skipped when collecting
                fixed (StbTrueType.stbtt_packedchar* cp = cjkPacked)
                {
                    StbTrueType.stbtt_PackFontRange(packContext, fontData, 0, fontSize,
                        FirstCjkChar, CjkCount, cp);
                }
            }
            StbTrueType.stbtt_PackEnd(packContext);
        }

        var dict = new Dictionary<int, GlyphInfo>(AsciiCount + CjkCount);
        CollectPacked(dict, asciiPacked, FirstAsciiChar);
        CollectPacked(dict, cjkPacked, FirstCjkChar);
        Glyphs = dict;
    }

    //CollectPacked collects packed chars into the dictionary, skipping glyphs that do not fit with x1<=x0 or y1<=y0
    private void CollectPacked(Dictionary<int, GlyphInfo> dict, StbTrueType.stbtt_packedchar[] packed, int firstChar)
    {
        for (int i = 0; i < packed.Length; i++)
        {
            var pc = packed[i];
            int x0 = pc.x0, y0 = pc.y0, x1 = pc.x1, y1 = pc.y1;
            if (x1 <= x0 || y1 <= y0) continue;
            int w = x1 - x0;
            int h = y1 - y0;
            float u0 = x0 / (float)AtlasWidth;
            float v0 = y0 / (float)AtlasHeight;
            float u1 = x1 / (float)AtlasWidth;
            float v1 = y1 / (float)AtlasHeight;
            dict[firstChar + i] = new GlyphInfo(x0, y0, w, h, pc.xoff, pc.yoff, pc.xadvance, u0, v0, u1, v1);
        }
    }

    //GetGlyph looks up a character and returns null when it is not in the atlas
    public GlyphInfo? GetGlyph(char c)
    {
        if (Glyphs.TryGetValue(c, out var info)) return info;
        if (Glyphs.TryGetValue('?', out var fallback)) return fallback;
        return null;
    }

    //MeasureText estimates the text pixel width
    public float MeasureText(string text)
    {
        float w = 0;
        foreach (var ch in text)
        {
            if (Glyphs.TryGetValue(ch, out var g))
                w += g.Advance;
            else if (Glyphs.TryGetValue('?', out var fb))
                w += fb.Advance;
        }
        return w;
    }

    //CandidateFontPaths common cross-platform TTF paths, preferring CJK fonts so Chinese renders
    private static IEnumerable<string> CandidateFontPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            //Prefer CJK fonts to ensure CJK glyphs exist
            yield return @"C:\Windows\Fonts\msyh.ttc";
            yield return @"C:\Windows\Fonts\msyh.ttf";
            yield return @"C:\Windows\Fonts\simhei.ttf";
            yield return @"C:\Windows\Fonts\simsun.ttc";
            yield return @"C:\Windows\Fonts\DENG.TTF";
            //Latin fallback
            yield return @"C:\Windows\Fonts\arial.ttf";
            yield return @"C:\Windows\Fonts\segoeui.ttf";
            yield return @"C:\Windows\Fonts\consola.ttf";
        }
        else if (OperatingSystem.IsLinux())
        {
            //Prefer CJK fonts
            yield return "/usr/share/fonts/wqy-microhei/wqy-microhei.ttc";
            yield return "/usr/share/fonts/truetype/wqy/wqy-microhei.ttc";
            yield return "/usr/share/fonts/wqy-zenhei/wqy-zenhei.ttc";
            yield return "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc";
            yield return "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc";
            yield return "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc";
            //Latin fallback
            yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
            yield return "/usr/share/fonts/dejavu/DejaVuSans.ttf";
            yield return "/usr/share/fonts/TTF/DejaVuSans.ttf";
            yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
        }
        else if (OperatingSystem.IsMacOS())
        {
            //Prefer CJK fonts
            yield return "/System/Library/Fonts/PingFang.ttc";
            yield return "/System/Library/Fonts/STHeiti Light.ttc";
            yield return "/System/Library/Fonts/Hiragino Sans GB.ttc";
            yield return "/Library/Fonts/Songti.ttc";
            //Latin fallback
            yield return "/System/Library/Fonts/Helvetica.ttc";
            yield return "/System/Library/Fonts/Menlo.ttc";
            yield return "/Library/Fonts/Arial.ttf";
        }
    }

    public void Dispose()
    {
        //The StbTrueType font bytes are held by _fontInfo and not released
    }
}
