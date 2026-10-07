using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//GlyphBlitRenderState glyph blit render state, maps to the 4-vertex quad submitted by vanilla BakedSheetGlyph.renderChar
//4 float vertices supporting italic shearTop/shearBottom and the bold extraThickness offset
//BlitRenderState uses int rectangles and does not support float offsets, so this type was added specifically for glyph rendering
//Participates in GuiRenderState sorting and batching, merging by (Pipeline, TextureSetup, ScissorArea)
public sealed record GlyphBlitRenderState(
    RenderPipeline Pipeline,
    TextureSetup TextureSetup,
    Matrix3x2 Pose,
    float X0, float Y0,
    float X1, float Y1,
    float X2, float Y2,
    float X3, float Y3,
    float U0, float V0,
    float U1, float V1,
    int Color,
    ScreenRectangle ScissorArea,
    ScreenRectangle Bounds) : GuiElementRenderState
{
    //The constructor overload without bounds derives it automatically from the 4 vertices+pose+scissor
    public GlyphBlitRenderState(
        RenderPipeline pipeline,
        TextureSetup textureSetup,
        Matrix3x2 pose,
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float u0, float v0, float u1, float v1,
        int color,
        ScreenRectangle scissorArea)
        : this(pipeline, textureSetup, pose, x0, y0, x1, y1, x2, y2, x3, y3, u0, v0, u1, v1, color, scissorArea,
            GetBounds(x0, y0, x1, y1, x2, y2, x3, y3, pose, scissorArea))
    {
    }

    //BuildVertices writes a 4-vertex quad matching the vanilla BakedSheetGlyph vertex order top-left→bottom-left→bottom-right→top-right
    //UV mapping top-left(U0,V0) bottom-left(U0,V1) bottom-right(U1,V1) top-right(U1,V0)
    public void BuildVertices(IVertexConsumer consumer)
    {
        consumer.AddVertexWith2DPose(Pose, X0, Y0, U0, V0, Color);
        consumer.AddVertexWith2DPose(Pose, X1, Y1, U0, V1, Color);
        consumer.AddVertexWith2DPose(Pose, X2, Y2, U1, V1, Color);
        consumer.AddVertexWith2DPose(Pose, X3, Y3, U1, V0, Color);
    }

    //GetBounds intersects the pose-transformed 4 vertices with the scissor for the final bounds
    private static ScreenRectangle GetBounds(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3, Matrix3x2 pose, ScreenRectangle scissorArea)
    {
        float minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
        float minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
        float maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
        float maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));
        var p0 = Vector2.Transform(new Vector2(minX, minY), pose);
        var p1 = Vector2.Transform(new Vector2(maxX, maxY), pose);
        var raw = new ScreenRectangle((int)Math.Floor(Math.Min(p0.X, p1.X)), (int)Math.Floor(Math.Min(p0.Y, p1.Y)),
            (int)Math.Ceiling(Math.Abs(p1.X - p0.X)), (int)Math.Ceiling(Math.Abs(p1.Y - p0.Y)));
        var intersection = scissorArea.Intersect(raw);
        return intersection ?? raw;
    }
}
