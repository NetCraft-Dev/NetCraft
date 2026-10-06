using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Advancements.Predicates;

//CollectionCountsPredicate collection counts predicate, maps to vanilla CollectionCountsPredicate
//Semantics: "the number of elements satisfying an entry falls in the given range"; degenerates into Zero/Single/Multiple by entry count
//A single entry requires that entry's count to hit the range; multiple entries require every entry's count to hit
public interface CollectionCountsPredicate<T, P> where P : class, IValuePredicate<T>
{
    //Unpack expands back to the entry list, used by the reverse direction of the codec
    IReadOnlyList<Entry> Unpack();

    //Test whether the collection satisfies
    bool Test(IEnumerable<T> values);

    //Codec persistence codec is just the entry list, maps to vanilla codec
    public static Codec<CollectionCountsPredicate<T, P>> Codec(Codec<P> elementCodec)
        => Entry.Codec(elementCodec).ListOf().ComapFlatMap(
            list => DataResult<CollectionCountsPredicate<T, P>>.Success(Of(list)),
            predicate => predicate.Unpack());

    //Of selects an implementation by entry count, maps to vanilla of
    public static CollectionCountsPredicate<T, P> Of(IReadOnlyList<Entry> predicates) => predicates.Count switch
    {
        0 => new Zero(),
        1 => new Single(predicates[0]),
        _ => new Multiple(predicates)
    };

    //Entry one entry, the test object plus a count range, maps to vanilla Entry
    public sealed record Entry(P Predicate, MinMaxBounds.Ints Count)
    {
        //Codec entry codec with the two fields test and count
        public static Codec<Entry> Codec(Codec<P> elementCodec) => RecordCodecBuilder.Of2(
            elementCodec.FieldOf("test").ForGetter((Entry entry) => entry.Predicate),
            MinMaxBounds.Ints.CODEC.FieldOf(ItemInstance.FieldCount).ForGetter((Entry entry) => entry.Count),
            (predicate, count) => new Entry(predicate, count));

        //Matches whether the number of elements satisfying this entry falls in the range
        public bool Matches(IEnumerable<T> values)
        {
            var count = 0;
            foreach (var value in values)
                if (Predicate.Test(value)) count++;
            return Count.Matches(count);
        }
    }

    //Zero empty entry list is always true
    public sealed class Zero : CollectionCountsPredicate<T, P>
    {
        public IReadOnlyList<Entry> Unpack() => Array.Empty<Entry>();

        public bool Test(IEnumerable<T> values) => true;
    }

    //Single one entry
    public sealed class Single(Entry entry) : CollectionCountsPredicate<T, P>
    {
        public Entry Value { get; } = entry;

        public IReadOnlyList<Entry> Unpack() => new[] { Value };

        public bool Test(IEnumerable<T> values) => Value.Matches(values);
    }

    //Multiple multiple entries, every one must hold
    public sealed class Multiple(IReadOnlyList<Entry> entries) : CollectionCountsPredicate<T, P>
    {
        public IReadOnlyList<Entry> Entries { get; } = entries;

        public IReadOnlyList<Entry> Unpack() => Entries;

        public bool Test(IEnumerable<T> values)
        {
            var list = values as IReadOnlyList<T> ?? values.ToList();
            foreach (var entry in Entries)
                if (!entry.Matches(list)) return false;
            return true;
        }
    }
}
