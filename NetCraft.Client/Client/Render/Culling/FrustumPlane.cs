using System.Numerics;

namespace NetCraft.Game.Client.Render.Culling;

//FrustumPlane 视锥剔除单平面对标原版 com.mojang.math.FrustumPlane
//Normal 法线 D 原点到平面距离 平面方程 Normal·P + D >= 0 表示点在平面内侧（可见侧）
public readonly struct FrustumPlane
{
    public readonly Vector3 Normal;
    public readonly float D;

    public FrustumPlane(Vector3 normal, float d)
    {
        Normal = normal;
        D = d;
    }

    //Distance 点到平面的有符号距离 正值在平面内侧（可见）负值在外侧（被剔除）
    public float Distance(Vector3 p) => Vector3.Dot(Normal, p) + D;
}
