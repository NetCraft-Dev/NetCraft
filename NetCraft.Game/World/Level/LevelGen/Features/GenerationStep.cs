namespace NetCraft.Game.World.Level.LevelGen.Features;

//GenerationStep generation step, maps to vanilla net.minecraft.world.level.levelgen.GenerationStep
//Decoration groups placed features; the index into a biome's features array is its ordinal
public static class GenerationStep
{
    //Decoration decoration steps, maps to vanilla GenerationStep.Decoration; there are 11
    //The order must not change: within one step structures are placed before features, and later steps depend on earlier output
    public enum Decoration
    {
        RawGeneration,
        Lakes,
        LocalModifications,
        UndergroundStructures,
        SurfaceStructures,
        Strongholds,
        UndergroundOres,
        UndergroundDecoration,
        FluidSprings,
        VegetalDecoration,
        TopLayerModification,
    }

    //Count total number of steps; decoration iterates by it
    public static readonly int Count = Enum.GetValues<Decoration>().Length;

    //Name the step name used in JSON, maps to vanilla getSerializedName
    public static string Name(this Decoration step) => step switch
    {
        Decoration.RawGeneration => "raw_generation",
        Decoration.Lakes => "lakes",
        Decoration.LocalModifications => "local_modifications",
        Decoration.UndergroundStructures => "underground_structures",
        Decoration.SurfaceStructures => "surface_structures",
        Decoration.Strongholds => "strongholds",
        Decoration.UndergroundOres => "underground_ores",
        Decoration.UndergroundDecoration => "underground_decoration",
        Decoration.FluidSprings => "fluid_springs",
        Decoration.VegetalDecoration => "vegetal_decoration",
        Decoration.TopLayerModification => "top_layer_modification",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
    };

    //TryParse parse a step from its JSON name; returns null for an invalid name
    public static Decoration? TryParse(string name)
    {
        foreach (var step in Enum.GetValues<Decoration>())
        {
            if (step.Name() == name) return step;
        }
        return null;
    }
}
