using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityNbtPredicate entity NBT predicate, checks whether the entity save data contains the expected tag
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityNbtPredicate
public sealed record EntityNbtPredicate(NbtPredicate Nbt) : EntitySubPredicate
{
    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<EntityNbtPredicate> Codec = NbtPredicate.Codec.ComapFlatMap(
        nbt => DataResult<EntityNbtPredicate>.Success(new EntityNbtPredicate(nbt)),
        predicate => predicate.Nbt);

    //Matches writes the entity to save data without the type id and compares, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        var tag = new CompoundTag();
        entity.SaveWithoutId(tag);
        return Nbt.Matches(tag);
    }
}
