using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//CaveSurface cave surface orientation, maps to vanilla CaveSurface
//Ceiling points up, floor points down; cave-style scans use it to locate
public enum CaveSurface
{
    Ceiling,
    Floor
}

//CaveSurfaceExtensions exposes equivalent access to the vanilla enum fields
public static class CaveSurfaceExtensions
{
    //GetDirection, maps to vanilla getDirection
    public static Direction GetDirection(this CaveSurface surface)
        => surface == CaveSurface.Ceiling ? Direction.Up : Direction.Down;

    //GetY, maps to vanilla getY; 1 for ceiling, -1 for floor
    public static int GetY(this CaveSurface surface) => surface == CaveSurface.Ceiling ? 1 : -1;

    //GetSerializedName, maps to vanilla getSerializedName
    public static string GetSerializedName(this CaveSurface surface)
        => surface == CaveSurface.Ceiling ? "ceiling" : "floor";

    //FromSerializedName reverse lookup by name, maps to vanilla StringRepresentable decoding
    public static CaveSurface? FromSerializedName(string name) => name switch
    {
        "ceiling" => CaveSurface.Ceiling,
        "floor" => CaveSurface.Floor,
        _ => null
    };
}
