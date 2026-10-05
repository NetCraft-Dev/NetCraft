using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityNbtPredicate 实体 NBT 谓词 判定实体存档数据是否包含期望标签
//对应原版 net.minecraft.advancements.predicates.entity.EntityNbtPredicate
public sealed record EntityNbtPredicate(NbtPredicate Nbt) : EntitySubPredicate
{
    //Codec 持久化编解码 对应原版 CODEC
    public static readonly Codec<EntityNbtPredicate> Codec = NbtPredicate.Codec.ComapFlatMap(
        nbt => DataResult<EntityNbtPredicate>.Success(new EntityNbtPredicate(nbt)),
        predicate => predicate.Nbt);

    //Matches 把实体写成不含类型 id 的存档再比对 对应原版 matches
    public bool Matches(NetCraft.Registry.Entity entity, Vec3? position)
    {
        var tag = new CompoundTag();
        entity.SaveWithoutId(tag);
        return Nbt.Matches(tag);
    }
}
