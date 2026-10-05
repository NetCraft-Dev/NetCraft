using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntitySubPredicates 实体子谓词注册 对应原版 net.minecraft.advancements.predicates.entity.EntitySubPredicates
//注册名沿用原版 未实现的类型待实体底座补齐后再登记
public static class EntitySubPredicates
{
    //Bootstrap 把已实现的子谓词登记进 ENTITY_SUB_PREDICATE_TYPE
    public static void Bootstrap()
    {
        Register("entity_type", EntityTypePredicate.Codec);
        Register("entity_tags", EntityTagPredicate.Codec);
        Register("flags", EntityFlagsPredicate.Codec);
        Register("nbt", EntityNbtPredicate.Codec);
        Register("movement", MovementPredicate.Codec);
        Register("distance", DistanceToPlayerPredicate.Codec);
        Register("periodic_tick", PeriodicEntityTickPredicate.Codec);
        Register("location", EntityLocationPredicate.Codec);
        Register("stepping_on", SteppingOnPredicate.Codec);
        Register("movement_affected_by", MovementAffectedByPredicate.Codec);
        Register("equipment", EntityEquipmentPredicate.Codec);
    }

    //Register 把具体子谓词的 codec 适配成接口版后按名登记
    private static void Register<T>(string name, Codec<T> codec) where T : class, EntitySubPredicate
        => Registry<Codec<EntitySubPredicate>>.Register(BuiltInRegistries.ENTITY_SUB_PREDICATE_TYPE, name,
            new EntitySubPredicateCodecAdapter<T>(codec));
}

//EntitySubPredicateCodecAdapter 把具体子谓词类型的 codec 适配成接口版 供子谓词注册表分派
internal sealed class EntitySubPredicateCodecAdapter<T> : ScalarCodec<EntitySubPredicate>
    where T : class, EntitySubPredicate
{
    private readonly Codec<T> _inner;

    public EntitySubPredicateCodecAdapter(Codec<T> inner) => _inner = inner;

    public override DataResult<EntitySubPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(value => (EntitySubPredicate)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, EntitySubPredicate value)
        => _inner.EncodeStart(ops, (T)value);
}
