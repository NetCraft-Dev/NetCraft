using System.Numerics;

namespace NetCraft.Client.Render.Culling;

//FrustumPlane a single frustum-culling plane, maps to vanilla com.mojang.math.FrustumPlane
//Normal n, D distance from origin to plane; the plane equation Normal·P + D >= 0 means the point is on the inner (visible) side
public readonly struct FrustumPlane
{
    public readonly Vector3 Normal;
    public readonly float D;

    public FrustumPlane(Vector3 normal, float d)
    {
        Normal = normal;
        D = d;
    }

    //Distance signed distance from a point to the plane; positive is on the inner side (visible), negative on the outer side (culled)
    public float Distance(Vector3 p) => Vector3.Dot(Normal, p) + D;
}
