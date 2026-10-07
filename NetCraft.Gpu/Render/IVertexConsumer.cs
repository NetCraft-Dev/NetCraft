using System.Numerics;

namespace NetCraft.Gpu;

//IVertexConsumer vertex consumer interface, maps to vanilla VertexConsumer
//RenderState.BuildVertices writes vertex data through this interface
//Stage 4 StagedVertexBuffer implements this interface to stage vertices
public interface IVertexConsumer
{
    //AddVertexWith2DPose adds a vertex with a 2D pose transform: position+UV+color
    //pose is baked into the vertex position here; multiple elements in one Draw may use different poses without affecting batching
    void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color);

    //AddVertex3D adds a 3D vertex position+color+uv+light+normal
    //position and normal are pre-transformed by the caller with PoseStack; the consumer only writes bytes per VertexFormat
    //color is an ARGB int and light is (block<<4)|(sky<<20) packed coords; both are written as float bit patterns
    //The default throws NotSupportedException; only the VertexBuilder of a 3D VertexFormat overrides it
    void AddVertex3D(float x, float y, float z, int color,
        float u, float v, int light, float nx, float ny, float nz)
        => throw new NotSupportedException("AddVertex3D is not implemented; this consumer does not support 3D vertices");
}
