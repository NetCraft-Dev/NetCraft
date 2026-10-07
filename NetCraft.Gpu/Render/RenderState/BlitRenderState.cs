using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//BlitRenderState single-texture blit render state, maps to vanilla BlitRenderState
//Immutable record carrying a full snapshot of pose/geometry/uv/color/scissor, participating in GuiRenderState sorting and batching
public sealed record BlitRenderState(
    RenderPipeline Pipeline,
    TextureSetup TextureSetup,
    Matrix3x2 Pose,
    int X0, int Y0, int X1, int Y1,
    float U0, float U1, float V0, float V1,
    int Color,
    ScreenRectangle ScissorArea,
    ScreenRectangle Bounds) : GuiElementRenderState
{
    //The constructor overload without bounds derives it automatically from the geometry+pose+scissor
    public BlitRenderState(
        RenderPipeline pipeline,
        TextureSetup textureSetup,
        Matrix3x2 pose,
        int x0, int y0, int x1, int y1,
        float u0, float u1, float v0, float v1,
        int color,
        ScreenRectangle scissorArea)
        : this(pipeline, textureSetup, pose, x0, y0, x1, y1, u0, u1, v0, v1, color, scissorArea,
            GetBounds(x0, y0, x1, y1, pose, scissorArea))
    {
    }

    //BuildVertices writes a 4-vertex quad; the pose is baked into the vertex positions here
    public void BuildVertices(IVertexConsumer consumer)
    {
        consumer.AddVertexWith2DPose(Pose, X0, Y0, U0, V0, Color);
        consumer.AddVertexWith2DPose(Pose, X0, Y1, U0, V1, Color);
        consumer.AddVertexWith2DPose(Pose, X1, Y1, U1, V1, Color);
        consumer.AddVertexWith2DPose(Pose, X1, Y0, U1, V0, Color);
    }

    //GetBounds intersects the pose-transformed geometry rectangle with the scissor for the final bounds
    private static ScreenRectangle GetBounds(int x0, int y0, int x1, int y1, Matrix3x2 pose, ScreenRectangle scissorArea)
    {
        var raw = new ScreenRectangle(x0, y0, x1 - x0, y1 - y0).TransformMaxBounds(pose);
        var intersection = scissorArea.Intersect(raw);
        return intersection ?? raw;
    }
}
