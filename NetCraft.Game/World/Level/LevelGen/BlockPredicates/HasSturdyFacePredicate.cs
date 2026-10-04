using NetCraft.Codec;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.LevelGen.BlockPredicates;

//HasSturdyFacePredicate 指定方向有坚固面对应原版 HasSturdyFacePredicate
public class HasSturdyFacePredicate : BlockPredicate
{
    public static readonly Codec<HasSturdyFacePredicate> Codec =
        RecordCodecBuilder.Of2<HasSturdyFacePredicate, Vec3i, Direction>(
            Vec3iCodec.Offset16.OptionalFieldOf("offset", Vec3i.Zero)
                .ForGetter<HasSturdyFacePredicate, Vec3i>(p => p._offset),
            DirectionCodec.Instance.FieldOf("direction")
                .ForGetter<HasSturdyFacePredicate, Direction>(p => p._direction),
            (offset, direction) => new HasSturdyFacePredicate(offset, direction));

    private readonly Vec3i _offset;
    private readonly Direction _direction;

    public HasSturdyFacePredicate(Vec3i offset, Direction direction)
    {
        _offset = offset;
        _direction = direction;
    }

    //NetCraft 没有方块形状信息 用不透明方块近似整面坚固
    public override bool Test(WorldGenRegion level, BlockPos origin)
    {
        var pos = origin.Offset(_offset);
        return level.GetBlockState(pos.X, pos.Y, pos.Z).GetLightDampening() >= 15;
    }

    public override BlockPredicateType Type => BlockPredicateType.HasSturdyFace;
}
