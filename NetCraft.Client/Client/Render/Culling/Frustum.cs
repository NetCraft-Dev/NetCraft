using System.Numerics;
using NetCraft.Primitives;

namespace NetCraft.Game.Client.Render.Culling;

//Frustum 6 平面视锥剔除器对标原版 com.mojang.blaze3d.frustum.Frustum
//Gribb-Hartmann 从 viewProj 矩阵提取 6 平面 适配 v*M 行向量乘语义（System.Numerics 惯例）
//Vulkan proj 矫正后 z_clip 范围 [0,w_clip] near 平面用 row3 far 平面用 row4-row3
//IsVisible 用 p-vertex 测试 AABB 沿法线正方向最远角点 全在外侧则剔除
public sealed class Frustum
{
    private readonly FrustumPlane[] _planes = new FrustumPlane[6];

    public Frustum(Matrix4x4 viewProj) => ExtractPlanes(viewProj);

    //ExtractPlanes Gribb-Hartmann 从 row-major viewProj 提取 6 平面
    //v*M 语义下 x_clip=v·row1 y_clip=v·row2 z_clip=v·row3 w_clip=v·row4
    //平面方程 a*x+b*y+c*z+d>=0 表示点在视锥内侧
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
        //near: z_clip >= 0（Vulkan z 范围 [0,1] 不用 OpenGL 的 z_clip+w_clip>=0）
        _planes[4] = Normalize(m.M13, m.M23, m.M33, m.M43);
        //far: w_clip - z_clip >= 0
        _planes[5] = Normalize(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);
    }

    //Normalize 归一化平面法线和距离避免不同平面量级不一致导致剔除偏差
    private static FrustumPlane Normalize(float a, float b, float c, float d)
    {
        var len = MathF.Sqrt(a * a + b * b + c * c);
        if (len < 1e-6f) return new FrustumPlane(Vector3.Zero, d);
        return new FrustumPlane(new Vector3(a / len, b / len, c / len), d / len);
    }

    //Prepare 设置相机偏移原版用于远距离浮点精度补偿 W5 简化相机位置已 bake 进 view 矩阵
    //W8 接入大世界坐标时改为相对相机偏移的 double 精度计算
    public void Prepare(double camX, double camY, double camZ)
    {
    }

    //IsVisible 测试 AABB 是否在视锥内 p-vertex 算法
    //对每个平面找 AABB 沿法线正方向最远的角点（p-vertex）若 p-vertex 在平面外侧则 AABB 完全在外
    public bool IsVisible(AABB box)
    {
        var minX = (float)box.Min.X; var minY = (float)box.Min.Y; var minZ = (float)box.Min.Z;
        var maxX = (float)box.Max.X; var maxY = (float)box.Max.Y; var maxZ = (float)box.Max.Z;
        for (var i = 0; i < 6; i++)
        {
            var plane = _planes[i];
            var n = plane.Normal;
            //p-vertex: 法线分量正取 Max 负取 Min 得到沿法线最远角点
            var px = n.X >= 0 ? maxX : minX;
            var py = n.Y >= 0 ? maxY : minY;
            var pz = n.Z >= 0 ? maxZ : minZ;
            if (n.X * px + n.Y * py + n.Z * pz + plane.D < 0)
                return false;
        }
        return true;
    }

    //IsPointVisible 测试单点是否在视锥内 6 平面全部在内侧才可见
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
