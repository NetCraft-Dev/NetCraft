using NetCraft.Codec;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//TrueBlockPredicate 恒真谓词对应原版 TrueBlockPredicate
public class TrueBlockPredicate : BlockPredicate
{
    public static readonly TrueBlockPredicate Instance = new();

    public static readonly Codec<TrueBlockPredicate> Codec = new UnitMapCodec<TrueBlockPredicate>(() => Instance);

    private TrueBlockPredicate() { }

    public override bool Test(WorldGenRegion level, BlockPos origin) => true;

    public override BlockPredicateType Type => BlockPredicateType.True;
}
