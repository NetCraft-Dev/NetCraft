using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//BlitRenderState 单张纹理 blit 渲染状态对标原版 BlitRenderState
//不可变 record 携带 pose/几何/uv/颜色/scissor 全量快照参与 GuiRenderState 排序合批
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
    //构造重载不传 bounds 时由几何+pose+scissor 自动推导
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

    //BuildVertices 写 4 顶点四边形 pose 在此处 bake 进顶点位置
    public void BuildVertices(IVertexConsumer consumer)
    {
        consumer.AddVertexWith2DPose(Pose, X0, Y0, U0, V0, Color);
        consumer.AddVertexWith2DPose(Pose, X0, Y1, U0, V1, Color);
        consumer.AddVertexWith2DPose(Pose, X1, Y1, U1, V1, Color);
        consumer.AddVertexWith2DPose(Pose, X1, Y0, U1, V0, Color);
    }

    //GetBounds 由几何矩形经 pose 变换后与 scissor 求交得出最终 Bounds
    private static ScreenRectangle GetBounds(int x0, int y0, int x1, int y1, Matrix3x2 pose, ScreenRectangle scissorArea)
    {
        var raw = new ScreenRectangle(x0, y0, x1 - x0, y1 - y0).TransformMaxBounds(pose);
        var intersection = scissorArea.Intersect(raw);
        return intersection ?? raw;
    }
}
