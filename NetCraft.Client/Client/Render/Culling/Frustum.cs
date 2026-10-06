using System.Numerics;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.Culling;

//Frustum 6-plane frustum culler, maps to vanilla com.mojang.blaze3d.frustum.Frustum
//Gribb-Hartmann extracts 6 planes from the viewProj matrix, adapted to v*M row-vector multiply semantics (System.Numerics convention)
//After Vulkan projection correction the z_clip range is [0,w_clip]; the near plane uses row3, the far plane uses row4-row3
//IsVisible uses the p-vertex test: the AABB corner farthest along the plane normal; if it is outside the plane, cull
public sealed class Frustum
{
    private readonly FrustumPlane[] _planes = new FrustumPlane[6];

    public Frustum(Matrix4x4 viewProj) => ExtractPlanes(viewProj);

    //ExtractPlanes Gribb-Hartmann extracts 6 planes from a row-major viewProj
    //Under v*M semantics x_clip=v·row1 y_clip=v·row2 z_clip=v·row3 w_clip=v·row4
    //The plane equation a*x+b*y+c*z+d>=0 means the point is inside the frustum
    private void ExtractPlanes(Matrix4x4 m)
    {
        //left: x_clip + w_clip >= 0
        _planes[0] = Normalize(m.M11 + m.M14, m.M21 + m.M24, m.M31 + m.M34, m.M41 + m.M44);
        //right: w_clip - x_clip >= 0
        _planes[1] = Normalize(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
        //bottom: y_clip + w_clip >= 0
        _planes[2] = Normalize(m.M12 + m.M14, m.M22 + m.M24, m.M32 + m.M34, m.M42 + m.M44);
        //top: w_clip - y_clip >= 0
        _planes[3] = Normalize(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
        //near: z_clip >= 0 (Vulkan z range [0,1]; not OpenGL's z_clip+w_clip>=0)
        _planes[4] = Normalize(m.M13, m.M23, m.M33, m.M43);
        //far: w_clip - z_clip >= 0
        _planes[5] = Normalize(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);
    }

    //Normalize normalizes the plane normal and distance to avoid culling bias from inconsistent plane magnitudes
    private static FrustumPlane Normalize(float a, float b, float c, float d)
    {
        var len = MathF.Sqrt(a * a + b * b + c * c);
        if (len < 1e-6f) return new FrustumPlane(Vector3.Zero, d);
        return new FrustumPlane(new Vector3(a / len, b / len, c / len), d / len);
    }

    //Prepare sets the camera offset; vanilla uses it for far-distance floating-point precision compensation. W5 simplified: the camera position is already baked into the view matrix
    //W8 will switch to double-precision computation relative to the camera offset when large-world coordinates are introduced
    public void Prepare(double camX, double camY, double camZ)
    {
    }

    //IsVisible tests whether an AABB is inside the frustum, p-vertex algorithm
    //For each plane, find the AABB corner farthest along the plane normal (p-vertex); if the p-vertex is outside the plane, the AABB is fully outside
    public bool IsVisible(AABB box)
    {
        var minX = (float)box.Min.X; var minY = (float)box.Min.Y; var minZ = (float)box.Min.Z;
        var maxX = (float)box.Max.X; var maxY = (float)box.Max.Y; var maxZ = (float)box.Max.Z;
        for (var i = 0; i < 6; i++)
        {
            var plane = _planes[i];
            var n = plane.Normal;
            //p-vertex: take Max where the normal component is positive and Min where negative, giving the corner farthest along the normal
            var px = n.X >= 0 ? maxX : minX;
            var py = n.Y >= 0 ? maxY : minY;
            var pz = n.Z >= 0 ? maxZ : minZ;
            if (n.X * px + n.Y * py + n.Z * pz + plane.D < 0)
                return false;
        }
        return true;
    }

    //IsPointVisible tests whether a single point is inside the frustum; visible only when on the inner side of all 6 planes
    public bool IsPointVisible(double x, double y, double z)
    {
        var p = new Vector3((float)x, (float)y, (float)z);
        for (var i = 0; i < 6; i++)
        {
            if (_planes[i].Distance(p) < 0)
                return false;
        }
        return true;
    }
}
