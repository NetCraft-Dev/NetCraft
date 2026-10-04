using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//BiomeFilter 群系过滤对应原版 BiomeFilter
//原版按位置查群系并校验特征归属 群系查询入口未接入前恒通过
public sealed class BiomeFilter : PlacementFilter
{
    public static readonly BiomeFilter Instance = new();

    public static readonly Codec<BiomeFilter> Codec = new UnitPlacementCodec<BiomeFilter>(() => Instance);

    private BiomeFilter() { }

    //Biome 构造入口对应原版 biome
    public static BiomeFilter Biome() => Instance;

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
        //占位: WorldGenRegion 未接入按位置读群系与特征的接口 暂时恒通过
        => true;

    public override PlacementModifierType Type => BiomeFilterType.Instance;
}

//BiomeFilterType 对应原版 PlacementModifierType.BIOME_FILTER
public sealed class BiomeFilterType : PlacementModifierType<BiomeFilter>
{
    public static readonly BiomeFilterType Instance = Register(
        Identifier.WithDefaultNamespace("biome"), new BiomeFilterType());

    private BiomeFilterType()
        : base(Identifier.WithDefaultNamespace("biome"), BiomeFilter.Codec) { }
}
