using NetCraft.Codec;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//AnyOfPredicate any match suffices, maps to vanilla AnyOfPredicate
public class AnyOfPredicate : CombiningPredicate
{
    public static readonly Codec<AnyOfPredicate> Codec = CreateCodec<AnyOfPredicate>(predicates => new AnyOfPredicate(predicates));

    public AnyOfPredicate(IReadOnlyList<BlockPredicate> predicates) : base(predicates) { }

    public override bool Test(WorldGenRegion level, BlockPos origin)
    {
        foreach (var predicate in Predicates)
            if (predicate.Test(level, origin)) return true;
        return false;
    }

    public override BlockPredicateType Type => BlockPredicateType.AnyOf;
}
