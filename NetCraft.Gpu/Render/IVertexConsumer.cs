using System.Numerics;

namespace NetCraft.Gpu;

//IVertexConsumer 顶点消费者接口对标原版 VertexConsumer
//RenderState.BuildVertices 通过此接口写入顶点数据
//阶段 4 StagedVertexBuffer 实现此接口暂存顶点
public interface IVertexConsumer
{
    //AddVertexWith2DPose 添加带 2D pose 变换的顶点位置+UV+颜色
    //pose 在此处 bake 进顶点位置同 Draw 内多元素可不同 pose 不影响合批
    void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color);

    //AddVertex3D 添加 3D 顶点 position+color+uv+light+normal
    //position 和 normal 由调用方用 PoseStack 预先变换好 consumer 只按 VertexFormat 写字节
    //color 是 ARGB int light 是 (block<<4)|(sky<<20) packed coords 都当 float 位模式写入
    //默认 throw NotSupportedException 仅供 3D VertexFormat 的 VertexBuilder 重写
    void AddVertex3D(float x, float y, float z, int color,
        float u, float v, int light, float nx, float ny, float nz)
        => throw new NotSupportedException("AddVertex3D 未实现此 consumer 不支持 3D 顶点");
}
