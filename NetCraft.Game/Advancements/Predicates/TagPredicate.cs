using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//TagPredicate tag predicate, checks whether a reference belongs to a tag, optionally requiring it to or not
//maps to vanilla net.minecraft.advancements.predicates.TagPredicate
public sealed record TagPredicate<T>(TagKey<T> Tag, bool Expected) where T : class
{
    //Codec persistence codec, field names id/expected, maps to vanilla codec
    public static Codec<TagPredicate<T>> Codec(ResourceKey<Registry<T>> registryKey) => RecordCodecBuilder.Of2(
        TagKey<T>.Codec(registryKey).FieldOf("id").ForGetter((TagPredicate<T> predicate) => predicate.Tag),
        Codecs.Bool.FieldOf("expected").ForGetter((TagPredicate<T> predicate) => predicate.Expected),
        (tag, expected) => new TagPredicate<T>(tag, expected));

    //Is requires the reference to belong to the tag, maps to vanilla is
    public static TagPredicate<T> Is(TagKey<T> tag) => new(tag, true);

    //IsNot requires the reference not to belong to the tag, maps to vanilla isNot
    public static TagPredicate<T> IsNot(TagKey<T> tag) => new(tag, false);

    //Matches the membership result equals the expectation, maps to vanilla matches
    public bool Matches(Holder<T> holder) => holder.Is(Tag) == Expected;
}
