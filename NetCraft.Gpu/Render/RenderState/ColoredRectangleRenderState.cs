using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//ColoredRectangleRenderState solid/two-color gradient rectangle render state, maps to vanilla ColoredRectangleRenderState
//col1 is used for the top-left/bottom-right vertices and col2 for the bottom-left/top-right, forming a diagonal gradient
//Immutable record participating in GuiRenderState sorting and batching
public sealed record ColoredRectangleRenderState(
    RenderPipeline Pipeline,
    TextureSetup TextureSetup,
    Matrix3x2 Pose,
    int X0, int Y0, int X1, int Y1,
    int Col1, int Col2,
    ScreenRectangle ScissorArea,
    ScreenRectangle Bounds) : GuiElementRenderState
{
    //The constructor overload without bounds derives it automatically from the geometry+pose+scissor
    public ColoredRectangleRenderState(
        RenderPipeline pipeline,
        TextureSetup textureSetup,
        Matrix3x2 pose,
        int x0, int y0, int x1, int y1,
        int col1, int col2,
        ScreenRectangle scissorArea)
        : this(pipeline, textureSetup, pose, x0, y0, x1, y1, col1, col2, scissorArea,
            GetBounds(x0, y0, x1, y1, pose, scissorArea))
    {
    }

    //BuildVertices writes a 4-vertex diagonal two-color quad with uv 0; the solid-color pipeline samples no texture
    public void BuildVertices(IVertexConsumer consumer)
    {
        consumer.AddVertexWith2DPose(Pose, X0, Y0, 0f, 0f, Col1);
        consumer.AddVertexWith2DPose(Pose, X0, Y1, 0f, 0f, Col2);
        consumer.AddVertexWith2DPose(Pose, X1, Y1, 0f, 0f, Col2);
        consumer.AddVertexWith2DPose(Pose, X1, Y0, 0f, 0f, Col1);
    }

    private static ScreenRectangle GetBounds(int x0, int y0, int x1, int y1, Matrix3x2 pose, ScreenRectangle scissorArea)
    {
        var raw = new ScreenRectangle(x0, y0, x1 - x0, y1 - y0).TransformMaxBounds(pose);
        var intersection = scissorArea.Intersect(raw);
        return intersection ?? raw;
    }
}
