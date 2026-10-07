using System.Numerics;

namespace NetCraft.Gpu;

//HumanoidModel humanoid entity model, maps to vanilla net.minecraft.client.model.HumanoidModel
//Part tree head/body/rightArm/leftArm/rightLeg/leftLeg each holding a single Cube, a simplified version
//SetupAnim drives the walk swing (limbSwing phase alternating the limbs) + head orientation
//Coordinate unit 1/16 block; the pivot and cube ranges match the vanilla model data
public sealed class HumanoidModel : EntityModel
{
    //Part pivot position model space 1/16 units
    //Head head, top at y=0 and neck at y=-8
    public ModelPart Head { get; }
    //Body torso, from y=0 to y=12
    public ModelPart Body { get; }
    //RightArm right arm pivot(-5,2,0) right shoulder
    public ModelPart RightArm { get; }
    //LeftArm left arm pivot(5,2,0) left shoulder
    public ModelPart LeftArm { get; }
    //RightLeg right leg pivot(-1.9,12,0) right hip
    public ModelPart RightLeg { get; }
    //LeftLeg left leg pivot(1.9,12,0) left hip
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

    //SetupAnim animation driver, maps to vanilla HumanoidModel.setupAnim
    //The head orientation in radians is applied directly; limbs swing by the limbSwing phase 0.6662 with amplitude speed and a π phase offset between left and right
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

    //CreateBody builds the humanoid part tree; root has no cube and only acts as a container
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

    //CreatePart creates a single-cube part with a pivot position + cube from/to range
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

    //CreateBoxPolygons generates the 6 face polygons of a from-to cube, vertices CCW seen from outside, UV 0-1
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
