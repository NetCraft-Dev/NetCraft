using NetCraft.Codec;
using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//UnobstructedPredicate the position has no collision obstruction, maps to vanilla UnobstructedPredicate
public sealed class UnobstructedPredicate : BlockPredicate
{
    public static readonly Codec<UnobstructedPredicate> Codec =
        new SingleFieldMapCodec<UnobstructedPredicate, Vec3i>(
            Vec3iCodec.Unbounded.OptionalFieldOf("offset", Vec3i.Zero),
            offset => new UnobstructedPredicate(offset), p => p.Offset);

    public UnobstructedPredicate(Vec3i offset) => Offset = offset;

    public Vec3i Offset { get; }

    //NetCraft has no VoxelShape, so air/fluid/replaceable blocks approximate no collision
    public override bool Test(WorldGenRegion level, BlockPos origin)
    {
        var pos = origin.Offset(Offset);
        var state = level.GetBlockState(pos.X, pos.Y, pos.Z);
        return state.Owner is BlockBehaviour behaviour
            && (behaviour.IsAir || behaviour.HasFluidState || behaviour.CanBeReplaced);
    }

    public override BlockPredicateType Type => BlockPredicateType.Unobstructed;
}
