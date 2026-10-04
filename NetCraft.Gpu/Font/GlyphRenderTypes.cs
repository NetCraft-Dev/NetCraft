using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu.Font;

//DisplayMode 字形渲染模式对标原版 Font.DisplayMode
//NORMAL 普通文本 SEE_THROUGH 看背穿透渲染 POLYGON_OFFSET 多边形偏移用于阴影
public enum DisplayMode
{
    Normal,
    SeeThrough,
    PolygonOffset
}

//GlyphRenderTypes 字形渲染管线集合对标原版 GlyphRenderTypes
//持 normal/seeThrough/polygonOffset 三种 RenderPipeline + guiPipeline
//createForGrayscaleTexture 返回灰度 pipeline 集用于 R8 图集
//createForColorTexture 返回彩色 pipeline 集用于 RGBA8 图集
//select(DisplayMode) 按 DisplayMode 返回对应 pipeline
public sealed class GlyphRenderTypes
{
    public RenderPipeline Normal { get; }
    public RenderPipeline SeeThrough { get; }
    public RenderPipeline PolygonOffset { get; }
    public RenderPipeline GuiPipeline { get; }

    public GlyphRenderTypes(RenderPipeline normal, RenderPipeline seeThrough, RenderPipeline polygonOffset, RenderPipeline guiPipeline)
    {
        Normal = normal;
        SeeThrough = seeThrough;
        PolygonOffset = polygonOffset;
        GuiPipeline = guiPipeline;
    }

    //createForGrayscaleTexture 灰度图集用 GUI_TEXT_GRAYSCALE 系列 pipeline
    public static GlyphRenderTypes CreateForGrayscaleTexture()
        => new(RenderPipelines.GUI_TEXT_GRAYSCALE,
            RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH,
            RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET,
            RenderPipelines.GUI_TEXT_GRAYSCALE);

    //createForColorTexture 彩色图集用 GUI_TEXT 系列 pipeline
    public static GlyphRenderTypes CreateForColorTexture()
        => new(RenderPipelines.GUI_TEXT,
            RenderPipelines.GUI_TEXT_SEE_THROUGH,
            RenderPipelines.GUI_TEXT_POLYGON_OFFSET,
            RenderPipelines.GUI_TEXT);

    //select 按 DisplayMode 返回对应 pipeline 对标原版 GlyphRenderTypes.select
    public RenderPipeline Select(DisplayMode mode) => mode switch
    {
        DisplayMode.Normal => Normal,
        DisplayMode.SeeThrough => SeeThrough,
        DisplayMode.PolygonOffset => PolygonOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
