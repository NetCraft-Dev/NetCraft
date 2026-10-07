using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//WouldSurvivePredicate whether the block can survive here, maps to vanilla WouldSurvivePredicate
public class WouldSurvivePredicate : BlockPredicate
{
    public static readonly Codec<WouldSurvivePredicate> Codec =
        RecordCodecBuilder.Of2<WouldSurvivePredicate, Vec3i, BlockState>(
            Vec3iCodec.Offset16.OptionalFieldOf("offset", Vec3i.Zero)
                .ForGetter<WouldSurvivePredicate, Vec3i>(p => p._offset),
            BlockStateCodec.Instance.FieldOf("state")
                .ForGetter<WouldSurvivePredicate, BlockState>(p => p._state),
            (offset, state) => new WouldSurvivePredicate(offset, state));

    private readonly Vec3i _offset;
    private readonly BlockState _state;

    public WouldSurvivePredicate(Vec3i offset, BlockState state)
    {
        _offset = offset;
        _state = state;
    }

    public Vec3i Offset => _offset;

    public BlockState State => _state;

    //NetCraft has no BlockState.canSurvive yet, so always true as a placeholder
    public override bool Test(WorldGenRegion level, BlockPos origin) => true;

    public override BlockPredicateType Type => BlockPredicateType.WouldSurvive;
}
