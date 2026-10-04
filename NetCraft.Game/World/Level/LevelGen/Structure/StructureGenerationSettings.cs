using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//TerrainAdjustment 结构周边地形适配方式 对应原版 TerrainAdjustment
//决定结构周围地形要不要被抬平 以及用哪种核函数 装饰阶段之外的噪声阶段会读它
public enum TerrainAdjustment
{
    None,
    Bury,
    BeardThin,
    BeardBox,
    Encapsulate,
}

//TerrainAdjustments 适配方式的实现与名字映射
public static class TerrainAdjustments
{
    //Name 取 JSON 名
    public static string Name(this TerrainAdjustment adjustment) => adjustment switch
    {
        TerrainAdjustment.Bury => "bury",
        TerrainAdjustment.BeardThin => "beard_thin",
        TerrainAdjustment.BeardBox => "beard_box",
        TerrainAdjustment.Encapsulate => "encapsulate",
        _ => "none",
    };

    //TryParse 按 JSON 名解析 非法返回 null
    public static TerrainAdjustment? TryParse(string name) => name switch
    {
        "none" => TerrainAdjustment.None,
        "bury" => TerrainAdjustment.Bury,
        "beard_thin" => TerrainAdjustment.BeardThin,
        "beard_box" => TerrainAdjustment.BeardBox,
        "encapsulate" => TerrainAdjustment.Encapsulate,
        _ => null,
    };

    //BeardEdgeNeeded 地形适配需要的额外边距 对应原版 12
    //结构包围盒要按它外扩 拼图的最大范围校验也用同一个 12
    public static int BeardEdgeNeeded(this TerrainAdjustment adjustment)
        => adjustment == TerrainAdjustment.None ? 0 : 12;
}

//StructureGenerationSettings 结构生成设置 对应原版 Structure.StructureSettings
//决定结构能长在哪些群系 挂在哪个装饰步骤 以及周边地形怎么适配
//原版还有 spawn_overrides 本作不做刷怪覆盖 相关字段不解析
public sealed record StructureGenerationSettings(
    HolderSet<Biome> Biomes,
    Features.GenerationStep.Decoration Step,
    TerrainAdjustment TerrainAdaptation)
{
    //Default 群系为空 步骤取地表结构 不改编地形
    public static readonly StructureGenerationSettings Default = new(
        DirectHolderSet<Biome>.Empty,
        Features.GenerationStep.Decoration.SurfaceStructures,
        TerrainAdjustment.None);
}
