using StbTrueTypeSharp;

namespace NetCraft.Gpu;

//GlyphInfo 单个字形在图集中的位置和度量
//U0/V0 是图集中字形左上角 UV，U1/V1 是右下角 UV
//Advance 是字符前进宽度，OffsetX/OffsetY 是字形相对于基线的偏移
//F7 标记 Obsolete 由 Font/GlyphStitcher/SheetBakedGlyph 动态烘焙路径替代保留作 fallback
[System.Obsolete("F7 由 Font 动态烘焙路径替代 GuiRenderContext 当 Font 非空时不再使用 FontAtlas")]
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

//FontAtlas 字形图集
//用 StbTrueType 加载 TTF 把 ASCII 32-126 + CJK U+4E00..U+9FFF 栅格化到 R8 单通道纹理
//CJK 范围大装不下的字跳过查找时回退到问号
//支持多分辨率重栅格化以便适配 DPI
//F7 标记 Obsolete 由 Font 动态烘焙路径替代保留作系统字体 fallback
[System.Obsolete("F7 由 Font/GlyphStitcher/SheetBakedGlyph 动态烘焙路径替代保留作系统字体 fallback")]
public sealed unsafe class FontAtlas : IDisposable
{
    private const int FirstAsciiChar = 32;
    private const int AsciiCount = 95;
    //CJK 统一表意文字 U+4E00..U+9FFF 覆盖常用汉字
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
    //Ascent 基线到字形顶部的距离 DrawText 转 baseline 用它而非 LineHeight
    //LineHeight = ascent - descent 比 ascent 多算 -descent 导致基线偏下中文最明显
    public int Ascent => _ascent;
    public float FontSize => _fontSize;
    public IReadOnlyDictionary<int, GlyphInfo> Glyphs { get; }

    //FromSystemFont 在常见系统字体路径中查找第一个可用的 TTF 加载
    //找不到返回 null 由调用者决定回退
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
                //当前路径字体加载失败试下一个
            }
        }
        return null;
    }

    //FromBytes 从 TTF 字节序列构造图集
    public FontAtlas(byte[] ttfBytes, float fontSize = DefaultFontSize)
    {
        _ttfBytes = ttfBytes;
        _fontSize = fontSize;
        _fontInfo = new StbTrueType.stbtt_fontinfo();
        fixed (byte* data = ttfBytes)
        {
            if (StbTrueType.stbtt_InitFont(_fontInfo, data, 0) == 0)
                throw new InvalidOperationException("stbtt_InitFont 失败");
        }

        int ascent, descent, lineGap;
        StbTrueType.stbtt_GetFontVMetrics(_fontInfo, &ascent, &descent, &lineGap);
        _ascent = ascent;
        _descent = descent;
        _lineGap = lineGap;
        LineHeight = _ascent - _descent;

        //atlas 加大到 2048x2048 容纳 CJK 常用汉字
        AtlasWidth = 2048;
        AtlasHeight = 2048;
        AtlasPixels = new byte[AtlasWidth * AtlasHeight];

        var asciiPacked = new StbTrueType.stbtt_packedchar[AsciiCount];
        var cjkPacked = new StbTrueType.stbtt_packedchar[CjkCount];
        var packContext = new StbTrueType.stbtt_pack_context();
        fixed (byte* pixels = AtlasPixels)
        {
            if (StbTrueType.stbtt_PackBegin(packContext, pixels, AtlasWidth, AtlasHeight, AtlasWidth, 1, null) == 0)
                throw new InvalidOperationException("stbtt_PackBegin 失败");

            fixed (byte* fontData = ttfBytes)
            {
                fixed (StbTrueType.stbtt_packedchar* ap = asciiPacked)
                {
                    if (StbTrueType.stbtt_PackFontRange(packContext, fontData, 0, fontSize,
                            FirstAsciiChar, AsciiCount, ap) == 0)
                        throw new InvalidOperationException("stbtt_PackFontRange ASCII 失败");
                }
                //CJK 范围大允许部分失败装不下的字在收集时跳过
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

    //CollectPacked 把 packedchar 收集进字典跳过 x1<=x0 或 y1<=y0 的装不下字形
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

    //GetGlyph 查找字符返回 null 表示不在图集中
    public GlyphInfo? GetGlyph(char c)
    {
        if (Glyphs.TryGetValue(c, out var info)) return info;
        if (Glyphs.TryGetValue('?', out var fallback)) return fallback;
        return null;
    }

    //MeasureText 估算文本像素宽度
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

    //CandidateFontPaths 跨平台常见 TTF 路径优先中文字体保证中文可显示
    private static IEnumerable<string> CandidateFontPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            //中文字体优先保证 CJK 字形存在
            yield return @"C:\Windows\Fonts\msyh.ttc";
            yield return @"C:\Windows\Fonts\msyh.ttf";
            yield return @"C:\Windows\Fonts\simhei.ttf";
            yield return @"C:\Windows\Fonts\simsun.ttc";
            yield return @"C:\Windows\Fonts\DENG.TTF";
            //英文回退
            yield return @"C:\Windows\Fonts\arial.ttf";
            yield return @"C:\Windows\Fonts\segoeui.ttf";
            yield return @"C:\Windows\Fonts\consola.ttf";
        }
        else if (OperatingSystem.IsLinux())
        {
            //中文字体优先
            yield return "/usr/share/fonts/wqy-microhei/wqy-microhei.ttc";
            yield return "/usr/share/fonts/truetype/wqy/wqy-microhei.ttc";
            yield return "/usr/share/fonts/wqy-zenhei/wqy-zenhei.ttc";
            yield return "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc";
            yield return "/usr/share/fonts/noto-cjk/NotoSansCJK-Regular.ttc";
            yield return "/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc";
            //英文回退
            yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
            yield return "/usr/share/fonts/dejavu/DejaVuSans.ttf";
            yield return "/usr/share/fonts/TTF/DejaVuSans.ttf";
            yield return "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf";
        }
        else if (OperatingSystem.IsMacOS())
        {
            //中文字体优先
            yield return "/System/Library/Fonts/PingFang.ttc";
            yield return "/System/Library/Fonts/STHeiti Light.ttc";
            yield return "/System/Library/Fonts/Hiragino Sans GB.ttc";
            yield return "/Library/Fonts/Songti.ttc";
            //英文回退
            yield return "/System/Library/Fonts/Helvetica.ttc";
            yield return "/System/Library/Fonts/Menlo.ttc";
            yield return "/Library/Fonts/Arial.ttf";
        }
    }

    public void Dispose()
    {
        //StbTrueType 字体字节由 _fontInfo 持有不释放
    }
}
