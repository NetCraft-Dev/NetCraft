using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//SteppingOnPredicate 踩踏位置谓词 判定实体脚下支撑方块所在位置
//对应原版 net.minecraft.advancements.predicates.entity.SteppingOnPredicate
public sealed record SteppingOnPredicate(LocationPredicate Location) : EntitySubPredicate
{
    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<SteppingOnPredicate> Codec = LocationPredicate.Codec.ComapFlatMap(
        location => DataResult<SteppingOnPredicate>.Success(new SteppingOnPredicate(location)),
        predicate => predicate.Location);

    //Matches 未着地或缺关卡判否 否则取脚下支撑方块中心坐标交给位置谓词 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        if (!entity.OnGround || level is null) return false;
        var onPos = entity.GetOnPos();
        return Location.Matches(level, onPos.X + 0.5, onPos.Y + 0.5, onPos.Z + 0.5);
    }
}
