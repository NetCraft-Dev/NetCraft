using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates.Entity;

//EntityTagPredicate entity string tag predicate, checks the containment relation of the entity's tag set
//maps to vanilla net.minecraft.advancements.predicates.entity.EntityTagPredicate
public sealed record EntityTagPredicate(
    Optional<IReadOnlyList<string>> AnyOf,
    Optional<IReadOnlyList<string>> AllOf,
    Optional<IReadOnlyList<string>> NoneOf) : EntitySubPredicate
{
    //Codec persistence codec, field names any_of/all_of/none_of, maps to vanilla CODEC
    public static readonly Codec<EntityTagPredicate> Codec = RecordCodecBuilder.Of3(
        Codecs.String.ListOf().OptionalFieldOf("any_of")
            .ForGetter((EntityTagPredicate predicate) => predicate.AnyOf),
        Codecs.String.ListOf().OptionalFieldOf("all_of")
            .ForGetter((EntityTagPredicate predicate) => predicate.AllOf),
        Codecs.String.ListOf().OptionalFieldOf("none_of")
            .ForGetter((EntityTagPredicate predicate) => predicate.NoneOf),
        (anyOf, allOf, noneOf) => new EntityTagPredicate(anyOf, allOf, noneOf));

    //Matches the tag set satisfies the any, none and all constraints at once, maps to vanilla matches
    public bool Matches(IReadOnlyCollection<string> tags)
    {
        if (AnyOf.IsPresent && !AnyOf.Get().Any(tags.Contains)) return false;
        if (NoneOf.IsPresent && NoneOf.Get().Any(tags.Contains)) return false;
        if (AllOf.IsPresent && !AllOf.Get().All(tags.Contains)) return false;
        return true;
    }

    //Matches fetches the entity tags then checks, maps to vanilla matches
    public bool Matches(NetCraft.Registry.Entity entity, ILevelReader? level, Vec3? position) => Matches(entity.GetTags());
}
