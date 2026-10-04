using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//MatchingBiomesPredicate 匹配群系对应原版 MatchingBiomesPredicate
public class MatchingBiomesPredicate : BlockPredicate
{
    //BiomeSet 群系集合 codec 对应原版 RegistryCodecs.homogeneousList(Registries.BIOME)
    private static readonly Codec<HolderSet<Biome>> BiomeSet =
        new RegistryHolderSetCodec<Biome>(BuiltInRegistries.BIOME);

    public static readonly Codec<MatchingBiomesPredicate> Codec =
        new SingleFieldMapCodec<MatchingBiomesPredicate, HolderSet<Biome>>(
            BiomeSet.FieldOf("biomes"), biomes => new MatchingBiomesPredicate(biomes), p => p._biomes);

    private readonly HolderSet<Biome> _biomes;

    public MatchingBiomesPredicate(HolderSet<Biome> biomes) => _biomes = biomes;

    public HolderSet<Biome> Biomes => _biomes;

    //NetCraft 还没有按位置查群系的入口 全部按匹配处理占位
    public override bool Test(WorldGenRegion level, BlockPos origin) => true;

    public override BlockPredicateType Type => BlockPredicateType.MatchingBiomes;
}
