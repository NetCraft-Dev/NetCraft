namespace NetCraft.Util.Random;

//Weighted list, maps to vanilla net.minecraft.util.random.WeightedList
//When total weight is below 64 use Flat array direct indexing, otherwise Compact cumulative search
public sealed class WeightedList<E>
{
    //FLAT_THRESHOLD below this uses the Flat strategy, maps to vanilla FLAT_THRESHOLD
    private const int FlatThreshold = 64;

    private readonly int _totalWeight;
    private readonly IReadOnlyList<Weighted<E>> _items;
    private readonly Selector<E>? _selector;

    //Selector selection strategy interface, maps to vanilla WeightedList.Selector
    private interface Selector<out T>
    {
        T Get(int selection);
    }

    //WeightedList constructor, maps to vanilla WeightedList(List)
    //Total weight zero makes selector null; below the threshold uses Flat, otherwise Compact
    public WeightedList(IEnumerable<Weighted<E>> items)
    {
        _items = items.ToList();
        _totalWeight = WeightedRandom.GetTotalWeight(_items, w => w.Weight);
        if (_totalWeight == 0)
            _selector = null;
        else if (_totalWeight < FlatThreshold)
            _selector = new FlatSelector<E>(_items, _totalWeight);
        else
            _selector = new CompactSelector<E>(_items);
    }

    //Empty empty list, maps to vanilla of
    public static WeightedList<E> Of() => new(Array.Empty<Weighted<E>>());

    //Of single value with weight 1, maps to vanilla of(E)
    public static WeightedList<E> Of(E value) => new(new[] { new Weighted<E>(value, 1) });

    //Of varargs, maps to vanilla of(E...)
    public static WeightedList<E> Of(params E[] items)
    {
        var builder = Builder();
        foreach (var item in items)
            builder.Add(item);
        return builder.Build();
    }

    //Of varargs Weighted, maps to vanilla of(Weighted...)
    public static WeightedList<E> Of(params Weighted<E>[] items) => new(items);

    //Of constructs from a List, maps to vanilla of(List)
    public static WeightedList<E> Of(IReadOnlyList<Weighted<E>> items)
        => new(items);

    //Builder builder entry, maps to vanilla builder
    //Nested class named BuilderImpl to avoid clashing with the Builder() method, CS0102
    public static BuilderImpl<E> Builder() => new();

    //IsEmpty empty when total weight is zero, maps to vanilla isEmpty
    public bool IsEmpty() => _selector is null;

    //map transforms the value type keeping the weight, maps to vanilla map
    public WeightedList<T> Map<T>(Func<E, T> mapper)
        => new(_items.Select(e => e.Map(mapper)));

    //GetRandom picks at random, maps to vanilla getRandom, returns Optional
    public Option<E> GetRandom(RandomSource random)
    {
        if (_selector is null)
            return Option<E>.None();
        var selection = random.NextInt(_totalWeight);
        return Option<E>.Some(_selector.Get(selection));
    }

    //GetRandomOrThrow picks at random, maps to vanilla getRandomOrThrow, throws on an empty list
    public E GetRandomOrThrow(RandomSource random)
    {
        if (_selector is null)
            throw new InvalidOperationException("Weighted list has no elements");
        var selection = random.NextInt(_totalWeight);
        return _selector.Get(selection);
    }

    //Unwrap returns the internal items, maps to vanilla unwrap
    public IReadOnlyList<Weighted<E>> Unwrap() => _items;

    //Contains whether a value exists, maps to vanilla contains
    public bool Contains(E value)
    {
        foreach (var item in _items)
            if (item.Value is null ? value is null : item.Value.Equals(value))
                return true;
        return false;
    }

    public override bool Equals(object? obj)
    {
        if (this == obj) return true;
        if (obj is not WeightedList<E> list) return false;
        return _totalWeight == list._totalWeight && _items.SequenceEqual(list._items);
    }

    public override int GetHashCode()
    {
        var hash = _totalWeight;
        foreach (var item in _items)
            hash = 31 * hash + (item?.GetHashCode() ?? 0);
        return hash;
    }

    //FlatSelector low-total-weight strategy, maps to vanilla WeightedList.Flat
    //Repeats values into an Object[] by weight for direct O(1) indexing
    private sealed class FlatSelector<T> : Selector<T>
    {
        private readonly object[] _entries;

        public FlatSelector(IReadOnlyList<Weighted<T>> entries, int totalWeight)
        {
            _entries = new object[totalWeight];
            var i = 0;
            foreach (var entry in entries)
            {
                var weight = entry.Weight;
                for (var j = 0; j < weight; j++)
                    _entries[i++] = entry.Value!;
            }
        }

        public T Get(int i) => (T)_entries[i];
    }

    //CompactSelector high-total-weight strategy, maps to vanilla WeightedList.Compact
    //Cumulative search by weight, trading space for time, O(n)
    private sealed class CompactSelector<T> : Selector<T>
    {
        private readonly Weighted<T>[] _entries;

        public CompactSelector(IReadOnlyList<Weighted<T>> entries)
            => _entries = entries.ToArray();

        public T Get(int i)
        {
            foreach (var weighted in _entries)
            {
                i -= weighted.Weight;
                if (i < 0)
                    return weighted.Value!;
            }
            throw new InvalidOperationException(i + " exceeded total weight");
        }
    }

    //Builder weight list builder, maps to vanilla WeightedList.Builder
    //Class named BuilderImpl to avoid clashing with the outer Builder() method, CS0102
    public sealed class BuilderImpl<E2>
    {
        private readonly List<Weighted<E2>> _result = new();

        //Add default weight 1, maps to vanilla add(E)
        public BuilderImpl<E2> Add(E2 item) => Add(item, 1);

        //Add with a given weight, maps to vanilla add(E,int)
        public BuilderImpl<E2> Add(E2 item, int weight)
        {
            _result.Add(new Weighted<E2>(item, weight));
            return this;
        }

        //Build constructs the WeightedList, maps to vanilla build
        public WeightedList<E2> Build() => new(_result);
    }
}

//Option lightweight Optional, maps to vanilla java.util.Optional
//random subdomain local use, avoids pulling in the public System.Linq types
public readonly struct Option<T>
{
    private readonly T? _value;
    public bool IsPresent { get; }

    private Option(T? value, bool isPresent)
    {
        _value = value;
        IsPresent = isPresent;
    }

    public T GetOrThrow() => IsPresent ? _value! : throw new InvalidOperationException("No value present");

    public static Option<T> Some(T value) => new(value, true);
    public static Option<T> None() => default;
}
