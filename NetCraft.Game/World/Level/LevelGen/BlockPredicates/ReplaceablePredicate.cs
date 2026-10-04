using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//ReplaceablePredicate 可替换判定对应原版 ReplaceablePredicate
public class ReplaceablePredicate : StateTestingPredicate
{
    public static readonly Codec<ReplaceablePredicate> Codec = new SingleFieldMapCodec<ReplaceablePredicate, Vec3i>(
        StateTestingCodec(), offset => new ReplaceablePredicate(offset), p => p.Offset);

    public ReplaceablePredicate(Vec3i offset) : base(offset) { }

    protected override bool Test(BlockState state)
        => state.Owner is BlockBehaviour behaviour && behaviour.CanBeReplaced;

    public override BlockPredicateType Type => BlockPredicateType.Replaceable;
}
