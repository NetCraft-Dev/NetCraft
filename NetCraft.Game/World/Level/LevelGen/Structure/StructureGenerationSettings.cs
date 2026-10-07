using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//TerrainAdjustment terrain adaptation mode around a structure, maps to vanilla TerrainAdjustment
//Decides whether the terrain around a structure gets raised flat and which kernel to use; the noise stage outside decoration reads it
public enum TerrainAdjustment
{
    None,
    Bury,
    BeardThin,
    BeardBox,
    Encapsulate,
}

//TerrainAdjustments adaptation mode implementation and name mapping
public static class TerrainAdjustments
{
    //Name returns the JSON name
    public static string Name(this TerrainAdjustment adjustment) => adjustment switch
    {
        TerrainAdjustment.Bury => "bury",
        TerrainAdjustment.BeardThin => "beard_thin",
        TerrainAdjustment.BeardBox => "beard_box",
        TerrainAdjustment.Encapsulate => "encapsulate",
        _ => "none",
    };

    //TryParse parses by JSON name, returns null when invalid
    public static TerrainAdjustment? TryParse(string name) => name switch
    {
        "none" => TerrainAdjustment.None,
        "bury" => TerrainAdjustment.Bury,
        "beard_thin" => TerrainAdjustment.BeardThin,
        "beard_box" => TerrainAdjustment.BeardBox,
        "encapsulate" => TerrainAdjustment.Encapsulate,
        _ => null,
    };

    //BeardEdgeNeeded extra margin needed by terrain adaptation, 12 in vanilla
    //The structure bounding box is inflated by it, and the jigsaw max range check uses the same 12
    public static int BeardEdgeNeeded(this TerrainAdjustment adjustment)
        => adjustment == TerrainAdjustment.None ? 0 : 12;
}

//StructureGenerationSettings structure generation settings, maps to vanilla Structure.StructureSettings
//Decides which biomes a structure can spawn in, which decoration step it hangs on, and how surrounding terrain adapts
//Vanilla also has spawn_overrides; this project does not implement mob spawn overrides, so those fields are not parsed
public sealed record StructureGenerationSettings(
    HolderSet<Biome> Biomes,
    Features.GenerationStep.Decoration Step,
    TerrainAdjustment TerrainAdaptation)
{
    //Default empty biomes, step set to surface structures, no terrain adaptation
    public static readonly StructureGenerationSettings Default = new(
        DirectHolderSet<Biome>.Empty,
        Features.GenerationStep.Decoration.SurfaceStructures,
        TerrainAdjustment.None);
}
