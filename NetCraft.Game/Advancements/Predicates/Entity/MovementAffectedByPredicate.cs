using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//MovementAffectedByPredicate 移动受何影响谓词 判定影响实体移动的那个方块所在位置
//对应原版 net.minecraft.advancements.predicates.entity.MovementAffectedByPredicate
public sealed record MovementAffectedByPredicate(LocationPredicate Location) : EntitySubPredicate
{
    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<MovementAffectedByPredicate> Codec = LocationPredicate.Codec.ComapFlatMap(
        location => DataResult<MovementAffectedByPredicate>.Success(new MovementAffectedByPredicate(location)),
        predicate => predicate.Location);

    //Matches 取影响移动的脚下方块中心坐标交给位置谓词 缺关卡时判否 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        if (level is null) return false;
        var onPos = entity.GetBlockPosBelowThatAffectsMyMovement();
        return Location.Matches(level, onPos.X + 0.5, onPos.Y + 0.5, onPos.Z + 0.5);
    }
}
