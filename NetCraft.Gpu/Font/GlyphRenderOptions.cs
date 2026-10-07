namespace NetCraft.Gpu.Font;

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
