using System.Numerics;

namespace NetCraft.Gpu;

//ModelPart model part, maps to vanilla net.minecraft.client.model.geom.ModelPart
//The entity model is a tree of ModelParts; each node holds a Cube list + a child Map
//render walks the tree: pushPose→translateAndRotate→compile cubes→recurse children→popPose
//Coordinate unit 1/16 block, cube side 16, matching the vanilla model space
public sealed class ModelPart
{
    //x/y/z part position in model space 1/16 units; translateAndRotate divides by 16
    public float X;
    public float Y;
    public float Z;
    //xRot/yRot/zRot part rotation in radians, ZYX order, maps to vanilla rotationZYX
    public float XRot;
    public float YRot;
    public float ZRot;
    //skipDraw when true skips compiling its own cubes but children still render, maps to vanilla
    public bool SkipDraw;
    //xScale/yScale/zScale part scale, maps to vanilla
    public float XScale = 1f;
    public float YScale = 1f;
    public float ZScale = 1f;
    //visible when false skips the whole subtree, maps to vanilla
    public bool Visible = true;

    private readonly List<Cube> _cubes;
    private readonly Dictionary<string, ModelPart> _children;

    public ModelPart(List<Cube> cubes, Dictionary<string, ModelPart> children)
    {
        _cubes = cubes;
        _children = children;
    }

    //HasChild whether a child with the given name exists
    public bool HasChild(string name) => _children.ContainsKey(name);
    //GetChild gets the child with the given name and throws if absent, maps to vanilla
    public ModelPart GetChild(string name) => _children.TryGetValue(name, out var c)
        ? c : throw new KeyNotFoundException($"ModelPart child node not found {name}");

    //Render renders this part and its subtree
    //poseStack transform stack builder vertex output lightCoords light coordinates overlayCoords overlay coordinates color color multiplier
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

    //TranslateAndRotate translates+rotates+scales the top of the stack
    //Position divided by 16 to block units, rotation in ZYX order, maps to vanilla rotationZYX
    public void TranslateAndRotate(PoseStack poseStack)
    {
        poseStack.Translate(X / 16f, Y / 16f, Z / 16f);
        if (XRot != 0f || YRot != 0f || ZRot != 0f)
        {
            //Vanilla mulPose right-multiplies Rz*Ry*Rx, applying X then Y then Z
            //CreateFromYawPitchRoll composes as Ry*Rx*Rz, whose order does not match, so Rz*Ry*Rx is built manually
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, ZRot)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitY, YRot)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitX, XRot);
            poseStack.Rotate(rotation);
        }
        if (XScale != 1f || YScale != 1f || ZScale != 1f)
            poseStack.Scale(XScale, YScale, ZScale);
    }

    //Compile compiles this part's Cube list, generating vertices into the builder
    //pose the model matrix transforms vertex positions normalMatrix the normal matrix transforms normals
    private void Compile(Matrix4x4 pose, Matrix4x4 normalMatrix, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
    {
        foreach (var cube in _cubes)
            cube.Compile(pose, normalMatrix, builder, lightCoords, overlayCoords, color);
    }

    //Cube model cube, maps to vanilla ModelPart.Cube
    //Made of 6 face Polygons with 4 vertices each; transformed to world space at compile and written to the builder
    public sealed class Cube
    {
        private readonly Polygon[] _polygons;

        public Cube(Polygon[] polygons) => _polygons = polygons;

        //Compile compiles all the cube's face vertices into the builder
        //pose transforms vertex positions and normalMatrix transforms normals; each face's 4 vertices expand into 2 triangles and 6 indices
        public void Compile(Matrix4x4 pose, Matrix4x4 normalMatrix, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
        {
            foreach (var polygon in _polygons)
            {
                var v = polygon.Vertices;
                //The 4 vertices transformed by the pose matrix
                var p0 = Vector3.Transform(v[0].Position, pose);
                var p1 = Vector3.Transform(v[1].Position, pose);
                var p2 = Vector3.Transform(v[2].Position, pose);
                var p3 = Vector3.Transform(v[3].Position, pose);
                //Normals transformed by the upper 3x3 of normalMatrix; the 4 vertices of a face share a normal so index 0 is used
                var normal = Vector3.TransformNormal(v[0].Normal, normalMatrix);
                builder.AddQuad(p0, p1, p2, p3, color,
                    v[0].U, v[0].V, v[2].U, v[2].V,
                    overlayCoords, lightCoords, normal);
            }
        }
    }

    //Polygon model face 4 vertices, maps to vanilla ModelPart.Polygon, order CCW seen from outside
    public sealed class Polygon(Vertex[] vertices)
    {
        public Vertex[] Vertices { get; } = vertices;
    }

    //Vertex model vertex, model space coordinates + UV + normal, maps to vanilla ModelPart.Vertex
    public readonly record struct Vertex(float X, float Y, float Z, float U, float V, float NX, float NY, float NZ)
    {
        //Position model space coordinates
        public Vector3 Position => new(X, Y, Z);
        //Normal normal in model space
        public Vector3 Normal => new(NX, NY, NZ);
    }
}
