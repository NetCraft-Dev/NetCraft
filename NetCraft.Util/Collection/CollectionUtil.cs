namespace NetCraft.Util.Collection;

//Collection utility static class, maps to the pure collection methods in vanilla net.minecraft.util.Util
//Ports Make/FindNext/MapValues/CopyAndAdd/Join/CopyAndPut/IsSymmetrical/GrowByHalf
public static class CollectionUtil
{
    //make builds from a factory and applies a configurator, maps to vanilla Util.make(Supplier)
    public static T Make<T>(Func<T> factory) => factory();

    //make applies a configurator to an existing value, maps to vanilla Util.make(T,Consumer)
    public static T Make<T>(T value, Action<T> configurator)
    {
        configurator(value);
        return value;
    }

    //findNextInIterable finds the next item after the current one, returns the first if not found, maps to vanilla findNextInIterable
    //Vanilla uses Iterator; C# iterates manually with IEnumerable and IEnumerator
    public static T FindNextInIterable<T>(IEnumerable<T> collection, T current)
    {
        using var iter = collection.GetEnumerator();
        if (!iter.MoveNext())
            throw new InvalidOperationException("Empty iterable");
        var first = iter.Current;
        if (current is null)
            return first;
        while (!EqualityComparer<T>.Default.Equals(iter.Current, current))
        {
            if (!iter.MoveNext())
                return first;
        }
        return iter.MoveNext() ? iter.Current : first;
    }

    //findPreviousInIterable finds the previous item before the current one, returns the last if not found, maps to vanilla findPreviousInIterable
    public static T FindPreviousInIterable<T>(IEnumerable<T> iterable, T t)
    {
        var list = iterable.ToList();
        if (list.Count == 0)
            throw new InvalidOperationException("Empty iterable");
        var idx = list.IndexOf(t);
        if (idx < 0)
            return list[^1];
        return idx == 0 ? list[^1] : list[idx - 1];
    }

    //mapValues transforms dictionary value types via a mapper, maps to vanilla Util.mapValues
    //Vanilla uses Stream.collect(Collectors.toMap); C# uses ToDictionary
    public static Dictionary<K, V2> MapValues<K, V1, V2>(IReadOnlyDictionary<K, V1> map, Func<V1, V2> valueMapper)
        where K : notnull
    {
        var result = new Dictionary<K, V2>(map.Count);
        foreach (var (key, value) in map)
            result[key] = valueMapper(value);
        return result;
    }

    //mapValuesLazy lazily transforms value types via a mapper, maps to vanilla Util.mapValuesLazy
    //Vanilla uses Guava Maps.transformValues to return a lazy view; C# wraps with ReadOnlyDictionary computing on demand
    public static IReadOnlyDictionary<K, V2> MapValuesLazy<K, V1, V2>(IReadOnlyDictionary<K, V1> map, Func<V1, V2> valueMapper)
        where K : notnull
        => new LazyMapDictionary<K, V1, V2>(map, valueMapper);

    //copyAndAdd copies a list, appends a single element and returns a new readonly list, maps to vanilla Util.copyAndAdd(List,T)
    public static IReadOnlyList<T> CopyAndAdd<T>(IReadOnlyList<T> list, T element)
    {
        var result = new List<T>(list.Count + 1);
        result.AddRange(list);
        result.Add(element);
        return result.AsReadOnly();
    }

    //copyAndAdd copies a list, appends multiple elements and returns a new readonly list, maps to vanilla Util.copyAndAdd(List,T...)
    public static IReadOnlyList<T> CopyAndAdd<T>(IReadOnlyList<T> list, params T[] elements)
    {
        var result = new List<T>(list.Count + elements.Length);
        result.AddRange(list);
        result.AddRange(elements);
        return result.AsReadOnly();
    }

    //copyAndAdd copies a list, prepends a single element and returns a new readonly list, maps to vanilla Util.copyAndAdd(T,List)
    public static IReadOnlyList<T> CopyAndAdd<T>(T element, IReadOnlyList<T> list)
    {
        var result = new List<T>(list.Count + 1) { element };
        result.AddRange(list);
        return result.AsReadOnly();
    }

    //join merges two lists into a new readonly list, maps to vanilla Util.join(List,List)
    public static IReadOnlyList<T> Join<T>(IReadOnlyList<T> first, IReadOnlyList<T> second)
    {
        var result = new List<T>(first.Count + second.Count);
        result.AddRange(first);
        result.AddRange(second);
        return result.AsReadOnly();
    }

    //join merges multiple lists into a new readonly list, maps to vanilla Util.join(List...)
    public static IReadOnlyList<T> Join<T>(params IReadOnlyList<T>[] lists)
    {
        var size = 0;
        foreach (var list in lists)
            size += list.Count;
        var result = new List<T>(size);
        foreach (var list in lists)
            result.AddRange(list);
        return result.AsReadOnly();
    }

    //copyAndPut copies a dictionary, adds a key-value pair and returns a new readonly dictionary, maps to vanilla Util.copyAndPut
    public static IReadOnlyDictionary<K, V> CopyAndPut<K, V>(IReadOnlyDictionary<K, V> map, K key, V value)
        where K : notnull
    {
        var result = new Dictionary<K, V>(map.Count + 1);
        foreach (var (k, v) in map)
            result[k] = v;
        result[key] = value;
        return result;
    }

    //isSymmetrical tests whether a matrix list is left-right symmetric, maps to vanilla Util.isSymmetrical
    //When width is 1 it is true directly, otherwise compares mirror elements row by row
    public static bool IsSymmetrical<T>(int width, int height, IReadOnlyList<T> ingredients)
    {
        if (width == 1)
            return true;
        var centerX = width / 2;
        for (var y = 0; y < height; y++)
        {
            for (var leftX = 0; leftX < centerX; leftX++)
            {
                var rightX = width - 1 - leftX;
                var left = ingredients[leftX + y * width];
                var right = ingredients[rightX + y * width];
                if (!EqualityComparer<T>.Default.Equals(left!, right!))
                    return false;
            }
        }
        return true;
    }

    //growByHalf grows by 1.5x, not exceeding int.MaxValue and not below the minimum, maps to vanilla Util.growByHalf
    public static int GrowByHalf(int currentSize, int minimalNewSize)
        => (int)Math.Max(Math.Min((long)currentSize + (currentSize >> 1), 2147483639L), minimalNewSize);

    //LazyMapDictionary lazy mapping dictionary, maps to vanilla Guava Maps.transformValues
    //Calls valueMapper on demand to avoid precomputing all values
    private sealed class LazyMapDictionary<K, V1, V2> : IReadOnlyDictionary<K, V2>
        where K : notnull
    {
        private readonly IReadOnlyDictionary<K, V1> _source;
        private readonly Func<V1, V2> _mapper;

        public LazyMapDictionary(IReadOnlyDictionary<K, V1> source, Func<V1, V2> mapper)
        {
            _source = source;
            _mapper = mapper;
        }

        public V2 this[K key] => _mapper(_source[key]);
        public IEnumerable<K> Keys => _source.Keys;
        public IEnumerable<V2> Values => _source.Values.Select(_mapper);
        public int Count => _source.Count;
        public bool ContainsKey(K key) => _source.ContainsKey(key);

        public bool TryGetValue(K key, out V2 value)
        {
            if (_source.TryGetValue(key, out var v1))
            {
                value = _mapper(v1);
                return true;
            }
            value = default!;
            return false;
        }

        public IEnumerator<KeyValuePair<K, V2>> GetEnumerator()
        {
            foreach (var (key, value) in _source)
                yield return new(key, _mapper(value));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
