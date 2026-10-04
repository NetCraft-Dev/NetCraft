namespace NetCraft.Gpu.Font;

//SheetBakedGlyph 烘焙到图集后的字形对标原版 BakedSheetGlyph
//持 IGlyphInfo 度量 + UV 坐标 + Left/Right/Top/Bottom 像素偏移 + TextureSetup + GlyphRenderTypes
//Render 完整实现 italic/bold/shadow 渲染逻辑对标原版 BakedSheetGlyph.renderChar
//italic 用 shearTop/shearBottom 公式 1.0-0.25*up/down bold 用 extraThickness=0.1 加粗二次绘制
//shadow 用 PolygonOffset pipeline 偏移 shadowOffset 绘制阴影色
internal sealed class SheetBakedGlyph : BakedGlyph
{
    public override IGlyphInfo Info { get; }

    //UV 图集纹理坐标内缩 0.01 像素避免采样越界
    internal readonly float U0;
    internal readonly float V0;
    internal readonly float U1;
    internal readonly float V1;

    //Left/Right/Top/Bottom 字形相对基线的像素偏移对标原版 BakedSheetGlyph left/right/up/down
    //Left/Top 是左上角偏移 Right/Bottom 是右下角偏移渲染时与 x/y 组合得四角顶点
    internal readonly float Left;
    internal readonly float Right;
    internal readonly float Top;
    internal readonly float Bottom;

    //TextureSetup 绑定字形图集纹理 Render 时传给 IGuiRenderContext.DrawGlyphQuad
    private readonly TextureSetup _textureSetup;
    //GlyphRenderTypes 三种 pipeline normal/seeThrough/polygonOffset Render 时按 DisplayMode 选取
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

    //Render 完整渲染逻辑对标原版 BakedSheetGlyph.renderChar
    //阴影用 PolygonOffset pipeline 偏移 shadowOffset 绘制阴影色 bold 时阴影也加粗
    //主字形用 Normal pipeline bold 时二次绘制偏移 boldOffset 加粗
    public override void Render(IGuiRenderContext context, in GlyphRenderOptions options)
    {
        float x = options.X;
        float y = options.Y;
        bool bold = options.Bold;
        bool italic = options.Italic;

        //阴影先绘制对标原版 renderChar hasShadow 时先 polygonOffset 阴影再 normal 主字形
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

        //主字形
        RenderSingle(context, x, y, options.Color, bold, italic, DisplayMode.Normal);
        if (bold)
        {
            RenderSingle(context, x + options.BoldOffset, y, options.Color, true, italic, DisplayMode.Normal);
        }
    }

    //RenderSingle 提交单个字形 quad 到渲染上下文
    //italic shearTop/shearBottom 公式 1.0-0.25*Top/Bottom bold extraThickness=0.1
    //4 顶点对标原版 左上→左下→右下→右上
    private void RenderSingle(IGuiRenderContext context, float x, float y, int color,
        bool bold, bool italic, DisplayMode mode)
    {
        //italic shear 对标原版 1.0 - 0.25 * up/down
        float shearTop = italic ? 1.0f - 0.25f * Top : 0.0f;
        float shearBottom = italic ? 1.0f - 0.25f * Bottom : 0.0f;
        //bold extraThickness 对标原版 bold 时 0.1f
        float extra = bold ? 0.1f : 0.0f;

        //4 顶点对标原版 BakedSheetGlyph.renderChar
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
