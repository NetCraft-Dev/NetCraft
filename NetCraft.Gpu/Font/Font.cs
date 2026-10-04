using System.Collections.Concurrent;

namespace NetCraft.Gpu.Font;

//GlyphFont 字体渲染入口对标原版 Font
//持 FontSet providers 链 + GlyphStitcher 烘焙器 + BakedGlyph 缓存
//Draw 遍历文本 codepoint 调 FontSet.GetGlyph → Bake → Render
//对标原版 Font.drawInBatch 阴影色由 RGB*0.25 计算保留 alpha
//Gpu 层不引入 Style 业务对象用 GlyphRenderOptions 封装渲染参数 bold/italic 由调用方传入
//F7 简化 Draw 只支持纯文本+shadow bold/italic 留待 Game 层富文本封装
//类名用 GlyphFont 避免与 NetCraft.Gpu.Font 命名空间同名冲突 C# 语言限制
public sealed class GlyphFont
{
    private readonly FontSet _fontSet;
    //F9 改为接口类型便于测试注入 mock stitcher GlyphFont 只用 Bake/GetMissing 不依赖 GlyphStitcher 具体实现
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

    //Draw 渲染文本 x/y 是顶部坐标 penY = y + Ascent 转基线
    //shadow=true 时 ShadowColor 非0 触发 BakedGlyph.Render 阴影绘制
    //color 是 ARGB int 对标原版 GlyphInstance.color
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
                //缺失字形用 GetMissing 占位对标原版 AllMissingGlyphProvider
                var missing = _stitcher.GetMissing();
                var options = new GlyphRenderOptions(penX, penY, color, shadowColor, false, false, 1.0f, 1.0f);
                missing.Render(context, in options);
                penX += missing.Info.Advance;
            }
        }
    }

    //MeasureText 测量文本像素宽度供控件计算对齐偏移
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

    //DrawGlyph 渲染单个字形返回 advance 供富文本遍历器累加 penX
    //对标原版 Font.drawInBatch 内单字形渲染 Game 层 FormattedTextRenderer 遍历 FormattedCharSequence 时调
    //codepoint 缺失时调 GetMissing 占位 SpecialGlyphs.Missing 紫色方块
    //options 由调用方按 Style 构造含 x/y/color/bold/italic 等 Gpu 层不依赖 Style
    public float DrawGlyph(IGuiRenderContext context, int codepoint, in GlyphRenderOptions options)
    {
        var baked = GetBaked(codepoint) ?? _stitcher.GetMissing();
        baked.Render(context, in options);
        return baked.Info.Advance;
    }

    //GetBaked codepoint → BakedGlyph 懒烘焙缓存对标原版 Font.getOrCreate 字形缓存
    private BakedGlyph? GetBaked(int codepoint)
    {
        return _bakedCache.GetOrAdd(codepoint, cp =>
        {
            var unbaked = _fontSet.GetGlyph(cp);
            if (unbaked == null) return null;
            return unbaked.Bake(_stitcher);
        });
    }

    //ComputeShadowColor 阴影色对标原版 Font.drawShadow 的 shadowColor 计算
    //原版把 color 的 RGB 乘以 0.25 保留 alpha
    private static int ComputeShadowColor(int color)
    {
        int a = (color >> 24) & 0xFF;
        int r = (int)(((color >> 16) & 0xFF) * 0.25f);
        int g = (int)(((color >> 8) & 0xFF) * 0.25f);
        int b = (int)((color & 0xFF) * 0.25f);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
