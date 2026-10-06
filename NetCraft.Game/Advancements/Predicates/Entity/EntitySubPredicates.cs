using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntitySubPredicates entity sub-predicate registration, maps to vanilla net.minecraft.advancements.predicates.entity.EntitySubPredicates
//Registry names follow vanilla; unimplemented types are registered once the entity base is complete
public static class EntitySubPredicates
{
    //Bootstrap registers the implemented sub-predicates into ENTITY_SUB_PREDICATE_TYPE
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
        Register("effects", EntityEffectsPredicate.Codec);
    }

    //Register adapts a concrete sub-predicate's codec into the interface version and registers it by name
    private static void Register<T>(string name, Codec<T> codec) where T : class, EntitySubPredicate
        => Registry<Codec<EntitySubPredicate>>.Register(BuiltInRegistries.ENTITY_SUB_PREDICATE_TYPE, name,
            new EntitySubPredicateCodecAdapter<T>(codec));
}

//EntitySubPredicateCodecAdapter adapts a concrete sub-predicate type's codec into the interface version, for the sub-predicate registry to dispatch
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
