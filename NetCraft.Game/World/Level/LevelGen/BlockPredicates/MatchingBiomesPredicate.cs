using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingBiomesPredicate matches biomes, maps to vanilla MatchingBiomesPredicate
public class MatchingBiomesPredicate : BlockPredicate
{
    //BiomeSet biome set codec, maps to vanilla RegistryCodecs.homogeneousList(Registries.BIOME)
    private static readonly Codec<HolderSet<Biome>> BiomeSet =
        new RegistryHolderSetCodec<Biome>(BuiltInRegistries.BIOME);

    public static readonly Codec<MatchingBiomesPredicate> Codec =
        new SingleFieldMapCodec<MatchingBiomesPredicate, HolderSet<Biome>>(
            BiomeSet.FieldOf("biomes"), biomes => new MatchingBiomesPredicate(biomes), p => p._biomes);

    private readonly HolderSet<Biome> _biomes;

    public MatchingBiomesPredicate(HolderSet<Biome> biomes) => _biomes = biomes;

    public HolderSet<Biome> Biomes => _biomes;

    //NetCraft has no position-based biome lookup yet, so everything matches as a placeholder
    public override bool Test(WorldGenRegion level, BlockPos origin) => true;

    public override BlockPredicateType Type => BlockPredicateType.MatchingBiomes;
}
