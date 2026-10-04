namespace NetCraft.Gpu.Font;

//GlyphRenderOptions 字形渲染参数对标原版 BakedSheetGlyph$GlyphInstance
//封装 x/y/color/shadowColor/bold/italic/boldOffset/shadowOffset
//替代原版 Style 业务对象 Gpu 层不引入富文本语义
//HasShadow 由 ShadowColor!=0 判断对标原版 hasShadow=shadowColor()!=0
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
    //HasShadow 阴影色非0表示需要绘制阴影对标原版 GlyphInstance.hasShadow
    public bool HasShadow => ShadowColor != 0;

    //Simple 创建无阴影无样式的普通字形参数
    public static GlyphRenderOptions Simple(float x, float y, int color)
        => new(x, y, color, 0, false, false, 1.0f, 1.0f);

    //WithShadow 创建带阴影的字形参数 shadowColor 阴影色 shadowOffset 偏移
    public GlyphRenderOptions WithShadow(int shadowColor, float shadowOffset)
        => this with { ShadowColor = shadowColor, ShadowOffset = shadowOffset };
}
