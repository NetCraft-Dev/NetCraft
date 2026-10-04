using NetCraft.Codec;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//NotPredicate 取反对应原版 NotPredicate
public class NotPredicate : BlockPredicate
{
    public static readonly Codec<NotPredicate> Codec = new SingleFieldMapCodec<NotPredicate, BlockPredicate>(
        BlockPredicate.Codec.FieldOf("predicate"), predicate => new NotPredicate(predicate), p => p.Predicate);

    private readonly BlockPredicate _predicate;

    public NotPredicate(BlockPredicate predicate) => _predicate = predicate;

    public BlockPredicate Predicate => _predicate;

    public override bool Test(WorldGenRegion level, BlockPos origin) => !_predicate.Test(level, origin);

    public override BlockPredicateType Type => BlockPredicateType.Not;
}
