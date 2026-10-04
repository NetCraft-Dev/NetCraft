using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//CaveSurface 洞穴表面朝向对应原版 CaveSurface
//天花板向上 地面向下 供洞穴类扫描定位
public enum CaveSurface
{
    Ceiling,
    Floor
}

//CaveSurfaceExtensions 提供原版枚举字段的等价访问
public static class CaveSurfaceExtensions
{
    //GetDirection 对应原版 getDirection
    public static Direction GetDirection(this CaveSurface surface)
        => surface == CaveSurface.Ceiling ? Direction.Up : Direction.Down;

    //GetY 对应原版 getY 天花板取 1 地面取 -1
    public static int GetY(this CaveSurface surface) => surface == CaveSurface.Ceiling ? 1 : -1;

    //GetSerializedName 对应原版 getSerializedName
    public static string GetSerializedName(this CaveSurface surface)
        => surface == CaveSurface.Ceiling ? "ceiling" : "floor";

    //FromSerializedName 按名字反查对应原版 StringRepresentable 解码
    public static CaveSurface? FromSerializedName(string name) => name switch
    {
        "ceiling" => CaveSurface.Ceiling,
        "floor" => CaveSurface.Floor,
        _ => null
    };
}
