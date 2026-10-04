using System.Numerics;

namespace NetCraft.Gpu;

//EntityVertexBuilder 实体顶点缓冲构造器
//ModelPart.compile 通过此接口写入 44 字节顶点 POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL
//11 float/顶点 position(3)+color(1)+uv(2)+overlay(1)+light(1)+normal(3)
//color/overlay/light 存 packed int 的 float 位模式 shader 用 intBitsToFloat 解包
//Indices 每 quad 6 索引 2 三角形 pipeline 拓扑 TriangleList
public sealed class EntityVertexBuilder
{
    //Vertices 顶点数据 11 float/顶点 连续存储供 MemoryMarshal.AsBytes 转 byte[] 上传 GPU
    public List<float> Vertices { get; } = new();
    //Indices 索引数据 每 quad 6 索引 供 DrawIndexed 使用
    public List<int> Indices { get; } = new();

    //VertexCount 顶点数
    public int VertexCount => Vertices.Count / 11;

    //AddVertex 添加一个实体顶点返回顶点索引供调用方构建 Indices
    //pos 已经过 PoseStack 变换的世界坐标 color/overlay/light 是 packed int
    public int AddVertex(Vector3 pos, int color, float u, float v, int overlay, int light, Vector3 normal)
    {
        var index = VertexCount;
        Vertices.Add(pos.X);
        Vertices.Add(pos.Y);
        Vertices.Add(pos.Z);
        //color/overlay/light 用 BitConverter.Int32BitsToSingle 存位模式 shader 解包
        Vertices.Add(BitConverter.Int32BitsToSingle(color));
        Vertices.Add(u);
        Vertices.Add(v);
        Vertices.Add(BitConverter.Int32BitsToSingle(overlay));
        Vertices.Add(BitConverter.Int32BitsToSingle(light));
        Vertices.Add(normal.X);
        Vertices.Add(normal.Y);
        Vertices.Add(normal.Z);
        return index;
    }

    //AddQuad 添加一个四边形 4 顶点 + 6 索引 2 三角形 CCW 从外侧看
    //4 顶点顺序 p0-p1-p2-p3 对应 UV (u0,v0)-(u1,v0)-(u1,v1)-(u0,v1)
    public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        int color, float u0, float v0, float u1, float v1,
        int overlay, int light, Vector3 normal)
    {
        var i0 = AddVertex(p0, color, u0, v0, overlay, light, normal);
        var i1 = AddVertex(p1, color, u1, v0, overlay, light, normal);
        var i2 = AddVertex(p2, color, u1, v1, overlay, light, normal);
        var i3 = AddVertex(p3, color, u0, v1, overlay, light, normal);
        Indices.Add(i0);
        Indices.Add(i1);
        Indices.Add(i2);
        Indices.Add(i0);
        Indices.Add(i2);
        Indices.Add(i3);
    }

    //AddQuad 按已烘焙四边形写顶点与索引 保留逐顶点 UV
    //方块的 BakedQuad 四个角 UV 不一定是轴对齐矩形 必须逐顶点写不能走 AddQuad 的两角形式
    //pose/normalMatrix 把 quad 的模型空间顶点与法线变换到世界空间
    public void AddQuad(in BakedQuad quad, Matrix4x4 pose, Matrix4x4 normalMatrix,
        int color, int overlay, int light)
    {
        var normal = Vector3.TransformNormal(quad.Direction.UnitVector(), normalMatrix);
        var i0 = AddVertex(Vector3.Transform(quad.P0, pose), color, quad.Uv0.X, quad.Uv0.Y, overlay, light, normal);
        var i1 = AddVertex(Vector3.Transform(quad.P1, pose), color, quad.Uv1.X, quad.Uv1.Y, overlay, light, normal);
        var i2 = AddVertex(Vector3.Transform(quad.P2, pose), color, quad.Uv2.X, quad.Uv2.Y, overlay, light, normal);
        var i3 = AddVertex(Vector3.Transform(quad.P3, pose), color, quad.Uv3.X, quad.Uv3.Y, overlay, light, normal);
        Indices.Add(i0);
        Indices.Add(i1);
        Indices.Add(i2);
        Indices.Add(i0);
        Indices.Add(i2);
        Indices.Add(i3);
    }

    //Clear 清空顶点和索引供 frame reuse
    public void Clear() { Vertices.Clear(); Indices.Clear(); }
}
