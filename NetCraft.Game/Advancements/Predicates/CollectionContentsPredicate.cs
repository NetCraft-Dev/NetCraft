using NetCraft.Codec;

namespace NetCraft.Game.Advancements.Predicates;

//CollectionContentsPredicate collection contents predicate, maps to vanilla CollectionContentsPredicate
//Semantics: some element in the collection satisfies an entry; degenerates into Zero/Single/Multiple by entry count
//A single entry means "one element satisfies"; multiple entries mean "each entry is hit by a different element"
public interface CollectionContentsPredicate<T, P> where P : class, IValuePredicate<T>
{
    //Unpack expands back to the entry list, used by the reverse direction of the codec
    IReadOnlyList<P> Unpack();

    //Test whether the collection satisfies
    bool Test(IEnumerable<T> values);

    //Codec persistence codec is just the entry list, maps to vanilla codec
    public static Codec<CollectionContentsPredicate<T, P>> Codec(Codec<P> elementCodec)
        => elementCodec.ListOf().ComapFlatMap(
            list => DataResult<CollectionContentsPredicate<T, P>>.Success(Of(list)),
            predicate => predicate.Unpack());

    //Of selects an implementation by entry count, maps to vanilla of
    public static CollectionContentsPredicate<T, P> Of(IReadOnlyList<P> predicates) => predicates.Count switch
    {
        0 => new Zero(),
        1 => new Single(predicates[0]),
        _ => new Multiple(predicates)
    };

    //Zero empty entry list is always true
    public sealed class Zero : CollectionContentsPredicate<T, P>
    {
        public IReadOnlyList<P> Unpack() => Array.Empty<P>();

        public bool Test(IEnumerable<T> values) => true;
    }

    //Single one entry, some element in the collection satisfies it
    public sealed class Single(P predicate) : CollectionContentsPredicate<T, P>
    {
        public P Predicate { get; } = predicate;

        public IReadOnlyList<P> Unpack() => new[] { Predicate };

        public bool Test(IEnumerable<T> values) => values.Any(Predicate.Test);
    }

    //Multiple multiple entries, each must be hit by a different element
    public sealed class Multiple(IReadOnlyList<P> tests) : CollectionContentsPredicate<T, P>
    {
        public IReadOnlyList<P> Tests { get; } = tests;

        public IReadOnlyList<P> Unpack() => Tests;

        public bool Test(IEnumerable<T> values)
        {
            var remaining = new List<P>(Tests);
            foreach (var value in values)
            {
                remaining.RemoveAll(item => item.Test(value));
                if (remaining.Count == 0) return true;
            }
            return false;
        }
    }
}
