using System.Numerics;

namespace NetCraft.Gpu;

//EntityVertexBuilder entity vertex buffer builder
//ModelPart.compile writes 44-byte vertices through this interface: POSITION_COLOR_TEX_OVERLAY_LIGHT_NORMAL
//11 floats/vertex position(3)+color(1)+uv(2)+overlay(1)+light(1)+normal(3)
//color/overlay/light store a packed int's float bit pattern; the shader unpacks with intBitsToFloat
//Indices 6 indices per quad, 2 triangles, pipeline topology TriangleList
public sealed class EntityVertexBuilder
{
    //Vertices vertex data 11 floats/vertex stored contiguously for MemoryMarshal.AsBytes to convert to byte[] for GPU upload
    public List<float> Vertices { get; } = new();
    //Indices index data 6 indices per quad, used by DrawIndexed
    public List<int> Indices { get; } = new();

    //VertexCount vertex count
    public int VertexCount => Vertices.Count / 11;

    //AddVertex adds an entity vertex and returns the vertex index for the caller to build Indices
    //pos is a world coordinate already transformed by PoseStack; color/overlay/light are packed ints
    public int AddVertex(Vector3 pos, int color, float u, float v, int overlay, int light, Vector3 normal)
    {
        var index = VertexCount;
        Vertices.Add(pos.X);
        Vertices.Add(pos.Y);
        Vertices.Add(pos.Z);
        //color/overlay/light use BitConverter.Int32BitsToSingle to store the bit pattern the shader unpacks
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

    //AddQuad adds a quad 4 vertices + 6 indices 2 triangles CCW seen from outside
    //The 4 vertices are ordered p0-p1-p2-p3 with UVs (u0,v0)-(u1,v0)-(u1,v1)-(u0,v1)
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

    //AddQuad writes vertices and indices from a baked quad, preserving per-vertex UVs
    //A block's BakedQuad corner UVs are not necessarily an axis-aligned rectangle, so write per-vertex instead of the two-corner AddQuad form
    //pose/normalMatrix transforms the quad's model-space vertices and normals into world space
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

    //Clear empties vertices and indices for frame reuse
    public void Clear() { Vertices.Clear(); Indices.Clear(); }
}
