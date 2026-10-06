using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Advancements.Predicates;

//CollectionPredicate collection predicate, size range plus contents predicate plus counts predicate, maps to vanilla net.minecraft.advancements.predicates.CollectionPredicate
//All three are optional; a missing one means unconstrained
public sealed record CollectionPredicate<T, P>(
    Optional<CollectionContentsPredicate<T, P>> Contains,
    Optional<CollectionCountsPredicate<T, P>> Counts,
    Optional<MinMaxBounds.Ints> Size) where P : class, IValuePredicate<T>
{
    //Codec persistence codec, field names contains/count/size, maps to vanilla codec
    public static Codec<CollectionPredicate<T, P>> Codec(Codec<P> elementCodec) => RecordCodecBuilder.Of3(
        CollectionContentsPredicate<T, P>.Codec(elementCodec).OptionalFieldOf("contains")
            .ForGetter((CollectionPredicate<T, P> predicate) => predicate.Contains),
        CollectionCountsPredicate<T, P>.Codec(elementCodec).OptionalFieldOf(ItemInstance.FieldCount)
            .ForGetter((CollectionPredicate<T, P> predicate) => predicate.Counts),
        MinMaxBounds.Ints.CODEC.OptionalFieldOf("size")
            .ForGetter((CollectionPredicate<T, P> predicate) => predicate.Size),
        (contains, counts, size) => new CollectionPredicate<T, P>(contains, counts, size));

    //Test whether the collection satisfies the size, contents and counts constraints at once
    public bool Test(IReadOnlyList<T> items)
    {
        if (Size.IsPresent && !Size.Get().Matches(items.Count)) return false;
        if (Contains.IsPresent && !Contains.Get().Test(items)) return false;
        if (Counts.IsPresent && !Counts.Get().Test(items)) return false;
        return true;
    }
}
