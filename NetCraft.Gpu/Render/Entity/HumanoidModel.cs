using System.Numerics;

namespace NetCraft.Gpu;

//HumanoidModel 人形实体模型对标原版 net.minecraft.client.model.HumanoidModel
//部件树 head/body/rightArm/leftArm/rightLeg/leftLeg 各持单 Cube 简化版
//SetupAnim 驱动行走摆动（limbSwing 相位四肢交替）+ 头部朝向
//坐标单位 1/16 块 pivot 与 cube 范围对齐原版模型数据
public sealed class HumanoidModel : EntityModel
{
    //部件 pivot 位置 model space 1/16 单位
    //Head 头部 头顶在 y=0 脖子在 y=-8
    public ModelPart Head { get; }
    //Body 躯干 从 y=0 到 y=12
    public ModelPart Body { get; }
    //RightArm 右臂 pivot(-5,2,0) 右肩
    public ModelPart RightArm { get; }
    //LeftArm 左臂 pivot(5,2,0) 左肩
    public ModelPart LeftArm { get; }
    //RightLeg 右腿 pivot(-1.9,12,0) 右髋
    public ModelPart RightLeg { get; }
    //LeftLeg 左腿 pivot(1.9,12,0) 左髋
    public ModelPart LeftLeg { get; }

    public HumanoidModel() : base(CreateBody())
    {
        Head = Root.GetChild("head");
        Body = Root.GetChild("body");
        RightArm = Root.GetChild("right_arm");
        LeftArm = Root.GetChild("left_arm");
        RightLeg = Root.GetChild("right_leg");
        LeftLeg = Root.GetChild("left_leg");
    }

    //SetupAnim 动画驱动对标原版 HumanoidModel.setupAnim
    //头部朝向弧度直接应用 四肢按 limbSwing 相位 0.6662 摆动幅度 speed 左右相位差 π
    public override void SetupAnim(EntityRenderState state)
    {
        Head.YRot = state.YRot;
        Head.XRot = state.XRot;
        Body.YRot = state.YBodyRot;
        var swing = state.WalkAnimationPos;
        var amount = state.WalkAnimationSpeed;
        LeftArm.XRot = MathF.Cos(swing * 0.6662f) * amount;
        RightArm.XRot = MathF.Cos(swing * 0.6662f + MathF.PI) * amount;
        LeftLeg.XRot = MathF.Cos(swing * 0.6662f + MathF.PI) * amount * 1.4f;
        RightLeg.XRot = MathF.Cos(swing * 0.6662f) * amount * 1.4f;
    }

    //CreateBody 构建人形部件树 root 无 cube 只作容器
    private static ModelPart CreateBody()
    {
        var children = new Dictionary<string, ModelPart>
        {
            { "head", CreatePart(0f, 0f, 0f, -4f, -8f, -4f, 4f, 0f, 4f) },
            { "body", CreatePart(0f, 0f, 0f, -4f, 0f, -2f, 4f, 12f, 2f) },
            { "right_arm", CreatePart(-5f, 2f, 0f, -8f, -2f, -2f, -4f, 10f, 2f) },
            { "left_arm", CreatePart(5f, 2f, 0f, -4f, -2f, -2f, 8f, 10f, 2f) },
            { "right_leg", CreatePart(-1.9f, 12f, 0f, -3.9f, 0f, -2f, 0.1f, 12f, 2f) },
            { "left_leg", CreatePart(1.9f, 12f, 0f, -0.1f, 0f, -2f, 3.9f, 12f, 2f) }
        };
        return new ModelPart(new List<ModelPart.Cube>(), children);
    }

    //CreatePart 创建单 cube 部件 pivot 位置 + cube from/to 范围
    private static ModelPart CreatePart(float pivotX, float pivotY, float pivotZ,
        float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
    {
        var cube = new ModelPart.Cube(CreateBoxPolygons(new Vector3(fromX, fromY, fromZ), new Vector3(toX, toY, toZ)));
        return new ModelPart(new List<ModelPart.Cube> { cube }, new Dictionary<string, ModelPart>())
        {
            X = pivotX,
            Y = pivotY,
            Z = pivotZ
        };
    }

    //CreateBoxPolygons 生成 from-to 立方体 6 面 polygon 顶点 CCW 从外侧看 UV 0-1
    private static ModelPart.Polygon[] CreateBoxPolygons(Vector3 from, Vector3 to)
    {
        var (x0, y0, z0) = (from.X, from.Y, from.Z);
        var (x1, y1, z1) = (to.X, to.Y, to.Z);
        var n = new[]
        {
            Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY,
            Vector3.UnitZ, -Vector3.UnitZ
        };
        var faces = new[]
        {
            //East +X
            (new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1)),
            //West -X
            (new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0)),
            //Up +Y
            (new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0)),
            //Down -Y
            (new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1)),
            //South +Z
            (new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1)),
            //North -Z
            (new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0)),
        };
        var polygons = new ModelPart.Polygon[6];
        for (int i = 0; i < 6; i++)
        {
            var (p0, p1, p2, p3) = faces[i];
            var normal = n[i];
            polygons[i] = new ModelPart.Polygon(new[]
            {
                new ModelPart.Vertex(p0.X, p0.Y, p0.Z, 0, 0, normal.X, normal.Y, normal.Z),
                new ModelPart.Vertex(p1.X, p1.Y, p1.Z, 1, 0, normal.X, normal.Y, normal.Z),
                new ModelPart.Vertex(p2.X, p2.Y, p2.Z, 1, 1, normal.X, normal.Y, normal.Z),
                new ModelPart.Vertex(p3.X, p3.Y, p3.Z, 0, 1, normal.X, normal.Y, normal.Z),
            });
        }
        return polygons;
    }
}
