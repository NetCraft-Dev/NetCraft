using System.Numerics;

namespace NetCraft.Gpu;

//Direction 面朝向对标原版 net.minecraft.core.Direction
//BakedQuad 用它存面法线 putBakedQuad 时取 UnitVector 作为法线方向
public enum Direction
{
    Down, Up, North, South, West, East
}

public static class DirectionExtensions
{
    //UnitVector 返回面法线单位向量
    public static Vector3 UnitVector(this Direction d) => d switch
    {
        Direction.Down => new Vector3(0, -1, 0),
        Direction.Up => new Vector3(0, 1, 0),
        Direction.North => new Vector3(0, 0, -1),
        Direction.South => new Vector3(0, 0, 1),
        Direction.West => new Vector3(-1, 0, 0),
        Direction.East => new Vector3(1, 0, 0),
        _ => Vector3.Zero
    };
}

//BakedQuad 烘焙四边形对标原版 BakedQuad record
//4 顶点位置 + 4 UV + 面朝向 + tintIndex 物品模型由 CubeModel 程序化生成
//法线是面级别 4 顶点共享来自 Direction 不存顶点法线
//PoC 简化 MaterialInfo 为 tintIndex + lightEmission 完整版有 sprite/layer/renderType
public readonly struct BakedQuad
{
    public readonly Vector3 P0, P1, P2, P3;
    public readonly Vector2 Uv0, Uv1, Uv2, Uv3;
    public readonly Direction Direction;
    public readonly int TintIndex;
    public readonly int LightEmission;

    public BakedQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
        Direction direction, int tintIndex = -1, int lightEmission = 0)
    {
        P0 = p0; P1 = p1; P2 = p2; P3 = p3;
        Uv0 = uv0; Uv1 = uv1; Uv2 = uv2; Uv3 = uv3;
        Direction = direction;
        TintIndex = tintIndex;
        LightEmission = lightEmission;
    }

    public Vector3 Position(int i) => i switch { 0 => P0, 1 => P1, 2 => P2, _ => P3 };
    public Vector2 Uv(int i) => i switch { 0 => Uv0, 1 => Uv1, 2 => Uv2, _ => Uv3 };
    public const int VertexCount = 4;
}

//QuadInstance 四边形运行时实例对标原版 QuadInstance
//putBakedQuad 时按顶点取 color 和 light PoC 简化为整四边形共享 color
public sealed class QuadInstance
{
    public int Color = -1;
    public int LightCoords;
    public int OverlayCoords;

    public int GetColor(int vertex) => Color;
    //GetLightCoordsWithEmission 合并方块自身发光到 blockLight 段不动 sky 段
    //emission 与 unpacked blockLight 取 max 后重新 pack 对齐原版 face light 合并语义
    public int GetLightCoordsWithEmission(int vertex, int lightEmission)
    {
        if (lightEmission <= 0) return LightCoords;
        var blockLight = LightTexture.UnpackBlockLight(LightCoords);
        var skyLight = LightTexture.UnpackSkyLight(LightCoords);
        if (lightEmission > blockLight) blockLight = lightEmission;
        return LightTexture.PackLightCoords(blockLight, skyLight);
    }
}
