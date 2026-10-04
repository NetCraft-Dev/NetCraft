using System.Numerics;

namespace NetCraft.Gpu;

//ModelPart 模型部件对标原版 net.minecraft.client.model.geom.ModelPart
//实体模型由树形 ModelPart 组成每节点持 Cube 列表 + 子节点 Map
//render 遍历树 pushPose→translateAndRotate→compile cubes→递归 children→popPose
//坐标单位 1/16 块方块边长 16 对标原版 model space
public sealed class ModelPart
{
    //x/y/z 部件位置 model space 1/16 单位 translateAndRotate 会除 16
    public float X;
    public float Y;
    public float Z;
    //xRot/yRot/zRot 部件旋转弧度 ZYX 顺序对标原版 rotationZYX
    public float XRot;
    public float YRot;
    public float ZRot;
    //skipDraw true 时跳过自身 Cube 编译但子节点仍渲染对标原版
    public bool SkipDraw;
    //xScale/yScale/zScale 部件缩放对标原版
    public float XScale = 1f;
    public float YScale = 1f;
    public float ZScale = 1f;
    //visible false 时整个子树跳过对标原版
    public bool Visible = true;

    private readonly List<Cube> _cubes;
    private readonly Dictionary<string, ModelPart> _children;

    public ModelPart(List<Cube> cubes, Dictionary<string, ModelPart> children)
    {
        _cubes = cubes;
        _children = children;
    }

    //HasChild 是否有指定名称的子节点
    public bool HasChild(string name) => _children.ContainsKey(name);
    //GetChild 获取指定名称子节点不存在抛异常对标原版
    public ModelPart GetChild(string name) => _children.TryGetValue(name, out var c)
        ? c : throw new KeyNotFoundException($"ModelPart 子节点不存在 {name}");

    //Render 渲染此部件及子树
    //poseStack 变换栈 builder 顶点输出 lightCoords 光照坐标 overlayCoords overlay 坐标 color 颜色乘数
    public void Render(PoseStack poseStack, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
    {
        if (!Visible) return;
        if (_cubes.Count == 0 && _children.Count == 0) return;
        poseStack.PushPose();
        TranslateAndRotate(poseStack);
        if (!SkipDraw) Compile(poseStack.Pose(), poseStack.Normal(), builder, lightCoords, overlayCoords, color);
        foreach (var child in _children.Values)
            child.Render(poseStack, builder, lightCoords, overlayCoords, color);
        poseStack.PopPose();
    }

    //TranslateAndRotate 平移+旋转+缩放栈顶
    //位置除 16 转方块单位旋转 ZYX 顺序对标原版 rotationZYX
    public void TranslateAndRotate(PoseStack poseStack)
    {
        poseStack.Translate(X / 16f, Y / 16f, Z / 16f);
        if (XRot != 0f || YRot != 0f || ZRot != 0f)
        {
            //原版 mulPose 右乘 Rz*Ry*Rx 应用顺序先 X 再 Y 再 Z
            //CreateFromYawPitchRoll 组合为 Ry*Rx*Rz 顺序不符需手搭 Rz*Ry*Rx
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, ZRot)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitY, YRot)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitX, XRot);
            poseStack.Rotate(rotation);
        }
        if (XScale != 1f || YScale != 1f || ZScale != 1f)
            poseStack.Scale(XScale, YScale, ZScale);
    }

    //Compile 编译此部件的 Cube 列表生成顶点写入 builder
    //pose 模型矩阵变换顶点位置 normalMatrix 法线矩阵变换法线
    private void Compile(Matrix4x4 pose, Matrix4x4 normalMatrix, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
    {
        foreach (var cube in _cubes)
            cube.Compile(pose, normalMatrix, builder, lightCoords, overlayCoords, color);
    }

    //Cube 模型立方体对标原版 ModelPart.Cube
    //由 6 面 Polygon 组成每面 4 顶点 compile 时变换到世界空间写入 builder
    public sealed class Cube
    {
        private readonly Polygon[] _polygons;

        public Cube(Polygon[] polygons) => _polygons = polygons;

        //Compile 编译立方体所有面顶点写入 builder
        //pose 变换顶点位置 normalMatrix 变换法线每面 4 顶点展开为 2 三角形 6 索引
        public void Compile(Matrix4x4 pose, Matrix4x4 normalMatrix, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
        {
            foreach (var polygon in _polygons)
            {
                var v = polygon.Vertices;
                //4 顶点经 pose 矩阵变换
                var p0 = Vector3.Transform(v[0].Position, pose);
                var p1 = Vector3.Transform(v[1].Position, pose);
                var p2 = Vector3.Transform(v[2].Position, pose);
                var p3 = Vector3.Transform(v[3].Position, pose);
                //法线经 normalMatrix 上 3x3 变换同一面 4 顶点法线相同取第 0 个
                var normal = Vector3.TransformNormal(v[0].Normal, normalMatrix);
                builder.AddQuad(p0, p1, p2, p3, color,
                    v[0].U, v[0].V, v[2].U, v[2].V,
                    overlayCoords, lightCoords, normal);
            }
        }
    }

    //Polygon 模型面 4 顶点对标原版 ModelPart.Polygon 顺序 CCW 从外侧看
    public sealed class Polygon(Vertex[] vertices)
    {
        public Vertex[] Vertices { get; } = vertices;
    }

    //Vertex 模型顶点 model space 坐标 + UV + 法线对标原版 ModelPart.Vertex
    public readonly record struct Vertex(float X, float Y, float Z, float U, float V, float NX, float NY, float NZ)
    {
        //Position model space 坐标
        public Vector3 Position => new(X, Y, Z);
        //Normal 法线 model space
        public Vector3 Normal => new(NX, NY, NZ);
    }
}
