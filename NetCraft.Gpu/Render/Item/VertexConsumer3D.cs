using System.Numerics;

namespace NetCraft.Gpu;

//VertexConsumer3D 3D 顶点消费者对标原版 VertexConsumer
//实现 IVertexConsumer 接口把 3D 顶点写入 List<float> 供 P14 链路 ItemPipRenderer/ItemItemAtlas 读字节上传 GPU
//PutBakedQuad 静态方法接受任意 IVertexConsumer 既可写 VertexConsumer3D 自身也可写 StagedVertexBuffer.VertexBuilder
//顶点格式 position(3f)+color(1f)+uv(2f)+light(1f)+normal(3f) 简化原版去掉 overlay
//color/light 是 int 数值转换写入 float shader 端 int() 还原 0xFFFFFFFF 位模式是 NaN 不能用位模式转换
public sealed class VertexConsumer3D : IVertexConsumer
{
    //Vertices 写入的顶点数据每个顶点 10 float position3+color1+uv2+light1+normal3
    //P14 链路用 MemoryMarshal.AsBytes 直接 memcpy 上传 GPU
    public readonly List<float> Vertices = new();

    //AddVertex3D 实现 IVertexConsumer 接口写 List<float>
    //position 和 normal 由 PutBakedQuad 预先变换好 color/light 数值转换写入
    public void AddVertex3D(float x, float y, float z, int color,
        float u, float v, int light, float nx, float ny, float nz)
    {
        //position3
        Vertices.Add(x); Vertices.Add(y); Vertices.Add(z);
        //color1 数值转换 0xFFFFFFFF -> -1.0f shader int(-1.0f)=-1=0xFFFFFFFF
        Vertices.Add((float)color);
        //uv2
        Vertices.Add(u); Vertices.Add(v);
        //light1 数值转换 light 值在 2^24 内 float 精确表示
        Vertices.Add((float)light);
        //normal3
        Vertices.Add(nx); Vertices.Add(ny); Vertices.Add(nz);
    }

    //PutBakedQuad 用 PoseStack 变换 BakedQuad 的 4 顶点后写入 consumer
    //position 用 pose 变换 normal 用 normalMatrix 变换后归一化
    //color/light 来自 QuadInstance emission 合并到 blockLight 段
    public static void PutBakedQuad(IVertexConsumer consumer, PoseStack poseStack, in BakedQuad quad, QuadInstance instance)
    {
        var pose = poseStack.Pose();
        var normalMatrix = poseStack.Normal();
        var normalVec = quad.Direction.UnitVector();
        var normal = Vector3.Normalize(Vector3.TransformNormal(normalVec, normalMatrix));
        var lightEmission = quad.LightEmission;
        for (var i = 0; i < BakedQuad.VertexCount; i++)
        {
            var pos = Vector3.Transform(quad.Position(i), pose);
            var uv = quad.Uv(i);
            var color = instance.GetColor(i);
            var light = instance.GetLightCoordsWithEmission(i, lightEmission);
            consumer.AddVertex3D(pos.X, pos.Y, pos.Z, color, uv.X, uv.Y, light, normal.X, normal.Y, normal.Z);
        }
    }

    public int VertexCount => Vertices.Count / 10;
    public void Clear() => Vertices.Clear();

    //AddVertexWith2DPose 2D 顶点 VertexConsumer3D 不支持显式抛异常
    void IVertexConsumer.AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color)
        => throw new NotSupportedException("VertexConsumer3D 不支持 2D 顶点");
}
