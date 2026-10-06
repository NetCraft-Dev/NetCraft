using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityPredicate entity predicate composite, merges several sub-predicates by type name into a single check
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityPredicate
//Vanilla wrap and createContext depend on the loot system, which is not wired up; this only implements checking and combination
public sealed class EntityPredicate
{
    //MapCodec mapping codec from type name to sub-predicate; the value codec is fetched by name from the registry, maps to vanilla MAP_CODEC
    private static readonly Codec<Dictionary<Identifier, EntitySubPredicate>> MapCodec =
        Codecs.DispatchedMap(IdentifierCodec.Instance, LookupCodec);

    //Codec persistence codec, maps to vanilla CODEC
    public static readonly Codec<EntityPredicate> Codec = MapCodec.ComapFlatMap(
        parts => DataResult<EntityPredicate>.Success(new EntityPredicate(parts)),
        predicate => new Dictionary<Identifier, EntitySubPredicate>(predicate.Parts));

    //Parts the sub-predicate set; keys are type names in the registry
    public IReadOnlyDictionary<Identifier, EntitySubPredicate> Parts { get; }

    //_combinedPart the merged single sub-predicate; checking goes through it
    private readonly EntitySubPredicate _combinedPart;

    public EntityPredicate(Dictionary<Identifier, EntitySubPredicate> parts)
    {
        Parts = parts;
        _combinedPart = Combine(parts);
    }

    //Matches when the entity is non-null it is handed to the merged sub-predicate, maps to vanilla matches
    public bool Matches(ILevelReader? level, Vec3? position, NetCraft.Registry.Entity? entity)
        => entity is not null && _combinedPart.Matches(entity, level, position);

    //LookupCodec fetches the registered codec from the registry by type name; an unregistered one returns an error codec
    private static Codec<EntitySubPredicate> LookupCodec(Identifier typeName)
        => BuiltInRegistries.ENTITY_SUB_PREDICATE_TYPE.GetValue(typeName)
            ?? new UnknownSubPredicateCodec(typeName.ToString());

    //Combine an empty map is always true; a single one is returned directly; multiple are sorted by type priority then conjoined, maps to vanilla combine
    private static EntitySubPredicate Combine(Dictionary<Identifier, EntitySubPredicate> parts)
    {
        if (parts.Count == 0) return TrueSubPredicate.Instance;
        if (parts.Count == 1) return parts.Values.First();
        var ordered = parts.OrderBy(entry => OrderOf(entry.Key)).Select(entry => entry.Value).ToList();
        return new CompositeSubPredicate(ordered);
    }

    //OrderOf the type predicate first, nbt last, the rest in between, maps to vanilla PREDICATE_TYPE_ORDER
    private static int OrderOf(Identifier typeName)
    {
        var path = typeName.Path;
        if (path == "entity_type") return -1;
        if (path == "nbt") return 2;
        return 0;
    }

    //Builder composite builder, maps to vanilla Builder
    public sealed class Builder
    {
        private readonly Dictionary<Identifier, EntitySubPredicate> _parts = new();

        public static Builder Entity() => new();

        //Put registers a sub-predicate by registry name, maps to vanilla put
        public Builder Put(string typeName, EntitySubPredicate predicate)
        {
            _parts[Identifier.Parse(typeName)] = predicate;
            return this;
        }

        //EntityType registers the entity type predicate, maps to vanilla entityType
        public Builder EntityType(EntityTypePredicate predicate) => Put("entity_type", predicate);

        //Tags registers the entity tag predicate, maps to vanilla entity_tags
        public Builder Tags(EntityTagPredicate predicate) => Put("entity_tags", predicate);

        //Flags registers the flags predicate, maps to vanilla flags
        public Builder Flags(EntityFlagsPredicate predicate) => Put("flags", predicate);

        //Nbt registers the NBT predicate, maps to vanilla nbt
        public Builder Nbt(EntityNbtPredicate predicate) => Put("nbt", predicate);

        //Moving registers the moving predicate, maps to vanilla moving
        public Builder Moving(MovementPredicate predicate) => Put("movement", predicate);

        //Distance registers the distance-to-initiator predicate, maps to vanilla distance
        public Builder Distance(DistanceToPlayerPredicate predicate) => Put("distance", predicate);

        //PeriodicTick registers the periodic tick predicate, maps to vanilla periodicTick
        public Builder PeriodicTick(PeriodicEntityTickPredicate predicate) => Put("periodic_tick", predicate);

        //Located registers the entity location predicate, maps to vanilla located
        public Builder Located(LocationPredicate location) => Put("location", new EntityLocationPredicate(location));

        //SteppingOn registers the stepping-on location predicate, maps to vanilla steppingOn
        public Builder SteppingOn(LocationPredicate location) => Put("stepping_on", new SteppingOnPredicate(location));

        //MovementAffectedBy registers the movement affected by predicate, maps to vanilla movementAffectedBy
        public Builder MovementAffectedBy(LocationPredicate location)
            => Put("movement_affected_by", new MovementAffectedByPredicate(location));

        //Equipment registers the equipment predicate, maps to vanilla equipment
        public Builder Equipment(EntityEquipmentPredicate equipment) => Put("equipment", equipment);

        //Effects registers the effects predicate, maps to vanilla effects
        public Builder Effects(EntityEffectsPredicate effects) => Put("effects", effects);

        //Build produces the composite, maps to vanilla build
        public EntityPredicate Build() => new(_parts);
    }
}

//TrueSubPredicate always-true sub-predicate, maps to vanilla EntitySubPredicate.ALWAYS_TRUE
internal sealed class TrueSubPredicate : EntitySubPredicate
{
    //Instance the single instance
    public static readonly TrueSubPredicate Instance = new();

    private TrueSubPredicate() { }

    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position) => true;
}

//CompositeSubPredicate conjunction of several sub-predicates, maps to vanilla the merge when combine has more than two
internal sealed class CompositeSubPredicate(IReadOnlyList<EntitySubPredicate> parts) : EntitySubPredicate
{
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position)
    {
        foreach (var part in parts)
            if (!part.Matches(entity, level, position)) return false;
        return true;
    }
}

//UnknownSubPredicateCodec unregistered sub-predicate type; both parsing and encoding error out
internal sealed class UnknownSubPredicateCodec(string typeName) : ScalarCodec<EntitySubPredicate>
{
    public override DataResult<EntitySubPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<EntitySubPredicate>.Error(() => $"unknown entity sub-predicate type {typeName}");

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, EntitySubPredicate value)
        => DataResult<U>.Error(() => $"unknown entity sub-predicate type {typeName}");
}
