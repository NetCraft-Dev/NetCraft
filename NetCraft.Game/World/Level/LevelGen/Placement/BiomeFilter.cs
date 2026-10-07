using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//BiomeFilter biome filter, maps to vanilla BiomeFilter
//Vanilla looks up the biome by position and validates feature ownership; always passes until biome lookup is wired up
public sealed class BiomeFilter : PlacementFilter
{
    public static readonly BiomeFilter Instance = new();

    public static readonly Codec<BiomeFilter> Codec = new UnitPlacementCodec<BiomeFilter>(() => Instance);

    private BiomeFilter() { }

    //Biome construction entry, maps to vanilla biome
    public static BiomeFilter Biome() => Instance;

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
        //Placeholder: WorldGenRegion has no position-based biome and feature lookup yet, so always pass
        => true;

    public override PlacementModifierType Type => BiomeFilterType.Instance;
}

//BiomeFilterType, maps to vanilla PlacementModifierType.BIOME_FILTER
public sealed class BiomeFilterType : PlacementModifierType<BiomeFilter>
{
    public static readonly BiomeFilterType Instance = Register(
        Identifier.WithDefaultNamespace("biome"), new BiomeFilterType());

    private BiomeFilterType()
        : base(Identifier.WithDefaultNamespace("biome"), BiomeFilter.Codec) { }
}
