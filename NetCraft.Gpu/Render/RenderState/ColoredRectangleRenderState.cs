using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//ColoredRectangleRenderState 纯色/双色渐变矩形渲染状态对标原版 ColoredRectangleRenderState
//col1 用于左上/右下顶点 col2 用于左下/右上顶点形成对角渐变
//不可变 record 参与 GuiRenderState 排序合批
public sealed record ColoredRectangleRenderState(
    RenderPipeline Pipeline,
    TextureSetup TextureSetup,
    Matrix3x2 Pose,
    int X0, int Y0, int X1, int Y1,
    int Col1, int Col2,
    ScreenRectangle ScissorArea,
    ScreenRectangle Bounds) : GuiElementRenderState
{
    //构造重载不传 bounds 时由几何+pose+scissor 自动推导
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

    //BuildVertices 写 4 顶点对角双色 uv 传 0 纯色管线不采样纹理
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
