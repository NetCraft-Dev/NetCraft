namespace NetCraft.Game.World.Level.LevelGen.Features;

//GenerationStep 生成步骤对应原版 net.minecraft.world.level.levelgen.GenerationStep
//Decoration 用来分组已放置特征 群系 features 数组的下标就是它的序数
public static class GenerationStep
{
    //Decoration 装饰步骤对应原版 GenerationStep.Decoration 共 11 个
    //顺序不可改 它决定同一步内结构先于特征放置 后面的步骤依赖前面步骤的产物
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

    //Count 步骤总数 装饰时按它推进
    public static readonly int Count = Enum.GetValues<Decoration>().Length;

    //Name 取 JSON 里的步骤名对应原版 getSerializedName
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

    //TryParse 按 JSON 名解析步骤 名字非法返回 null
    public static Decoration? TryParse(string name)
    {
        foreach (var step in Enum.GetValues<Decoration>())
        {
            if (step.Name() == name) return step;
        }
        return null;
    }
}
