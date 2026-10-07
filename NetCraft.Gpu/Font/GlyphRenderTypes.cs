using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu.Font;

//DisplayMode glyph render mode, maps to vanilla Font.DisplayMode
//NORMAL plain text SEE_THROUGH see-through rendering POLYGON_OFFSET polygon offset used for shadows
public enum DisplayMode
{
    Normal,
    SeeThrough,
    PolygonOffset
}

//GlyphRenderTypes glyph render pipeline set, maps to vanilla GlyphRenderTypes
//Holds the three RenderPipelines normal/seeThrough/polygonOffset + guiPipeline
//createForGrayscaleTexture returns the grayscale pipeline set for R8 atlases
//createForColorTexture returns the color pipeline set for RGBA8 atlases
//select(DisplayMode) returns the matching pipeline for a DisplayMode
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

    //createForGrayscaleTexture grayscale atlases use the GUI_TEXT_GRAYSCALE pipeline series
    public static GlyphRenderTypes CreateForGrayscaleTexture()
        => new(RenderPipelines.GUI_TEXT_GRAYSCALE,
            RenderPipelines.GUI_TEXT_GRAYSCALE_SEE_THROUGH,
            RenderPipelines.GUI_TEXT_GRAYSCALE_POLYGON_OFFSET,
            RenderPipelines.GUI_TEXT_GRAYSCALE);

    //createForColorTexture color atlases use the GUI_TEXT pipeline series
    public static GlyphRenderTypes CreateForColorTexture()
        => new(RenderPipelines.GUI_TEXT,
            RenderPipelines.GUI_TEXT_SEE_THROUGH,
            RenderPipelines.GUI_TEXT_POLYGON_OFFSET,
            RenderPipelines.GUI_TEXT);

    //select returns the matching pipeline for a DisplayMode, maps to vanilla GlyphRenderTypes.select
    public RenderPipeline Select(DisplayMode mode) => mode switch
    {
        DisplayMode.Normal => Normal,
        DisplayMode.SeeThrough => SeeThrough,
        DisplayMode.PolygonOffset => PolygonOffset,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
