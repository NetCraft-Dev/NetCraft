using NetCraft.Codec;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//AllOfPredicate all must match, maps to vanilla AllOfPredicate
public class AllOfPredicate : CombiningPredicate
{
    public static readonly Codec<AllOfPredicate> Codec = CreateCodec<AllOfPredicate>(predicates => new AllOfPredicate(predicates));

    public AllOfPredicate(IReadOnlyList<BlockPredicate> predicates) : base(predicates) { }

    public override bool Test(WorldGenRegion level, BlockPos origin)
    {
        foreach (var predicate in Predicates)
            if (!predicate.Test(level, origin)) return false;
        return true;
    }

    public override BlockPredicateType Type => BlockPredicateType.AllOf;
}
