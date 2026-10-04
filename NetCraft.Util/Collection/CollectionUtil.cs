namespace NetCraft.Util.Collection;

//集合工具静态类对应原版net.minecraft.util.Util中纯集合方法
//移植Make/FindNext/MapValues/CopyAndAdd/Join/CopyAndPut/IsSymmetrical/GrowByHalf
public static class CollectionUtil
{
    //make按工厂构造并应用配置器对应原版Util.make(Supplier)
    public static T Make<T>(Func<T> factory) => factory();

    //make按已有值应用配置器对应原版Util.make(T,Consumer)
    public static T Make<T>(T value, Action<T> configurator)
    {
        configurator(value);
        return value;
    }

    //findNextInIterable找当前元素下一项找不到返回第一项对应原版findNextInIterable
    //原版用Iterator实现C#用IEnumerable加IEnumerator手动迭代
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

    //findPreviousInIterable找当前元素前一项找不到返回末项对应原版findPreviousInIterable
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

    //mapValues按映射函数转换字典值类型对应原版Util.mapValues
    //原版用Stream.collect(Collectors.toMap)C#用ToDictionary
    public static Dictionary<K, V2> MapValues<K, V1, V2>(IReadOnlyDictionary<K, V1> map, Func<V1, V2> valueMapper)
        where K : notnull
    {
        var result = new Dictionary<K, V2>(map.Count);
        foreach (var (key, value) in map)
            result[key] = valueMapper(value);
        return result;
    }

    //mapValuesLazy按映射函数惰性转换值类型对应原版Util.mapValuesLazy
    //原版用Guava Maps.transformValues返回lazy view C#用ReadOnlyDictionary包装按需计算
    public static IReadOnlyDictionary<K, V2> MapValuesLazy<K, V1, V2>(IReadOnlyDictionary<K, V1> map, Func<V1, V2> valueMapper)
        where K : notnull
        => new LazyMapDictionary<K, V1, V2>(map, valueMapper);

    //copyAndAdd复制列表追加单个元素返回新只读列表对应原版Util.copyAndAdd(List,T)
    public static IReadOnlyList<T> CopyAndAdd<T>(IReadOnlyList<T> list, T element)
    {
        var result = new List<T>(list.Count + 1);
        result.AddRange(list);
        result.Add(element);
        return result.AsReadOnly();
    }

    //copyAndAdd复制列表追加多个元素返回新只读列表对应原版Util.copyAndAdd(List,T...)
    public static IReadOnlyList<T> CopyAndAdd<T>(IReadOnlyList<T> list, params T[] elements)
    {
        var result = new List<T>(list.Count + elements.Length);
        result.AddRange(list);
        result.AddRange(elements);
        return result.AsReadOnly();
    }

    //copyAndAdd复制列表前插单个元素返回新只读列表对应原版Util.copyAndAdd(T,List)
    public static IReadOnlyList<T> CopyAndAdd<T>(T element, IReadOnlyList<T> list)
    {
        var result = new List<T>(list.Count + 1) { element };
        result.AddRange(list);
        return result.AsReadOnly();
    }

    //join合并两个列表返回新只读列表对应原版Util.join(List,List)
    public static IReadOnlyList<T> Join<T>(IReadOnlyList<T> first, IReadOnlyList<T> second)
    {
        var result = new List<T>(first.Count + second.Count);
        result.AddRange(first);
        result.AddRange(second);
        return result.AsReadOnly();
    }

    //join合并多个列表返回新只读列表对应原版Util.join(List...)
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

    //copyAndPut复制字典追加键值对返回新只读字典对应原版Util.copyAndPut
    public static IReadOnlyDictionary<K, V> CopyAndPut<K, V>(IReadOnlyDictionary<K, V> map, K key, V value)
        where K : notnull
    {
        var result = new Dictionary<K, V>(map.Count + 1);
        foreach (var (k, v) in map)
            result[k] = v;
        result[key] = value;
        return result;
    }

    //isSymmetrical判断矩阵列表左右对称对应原版Util.isSymmetrical
    //width为1直接true其余按行比对左右镜像元素
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

    //growByHalf按1.5倍扩容不超int最大值不小于最小值对应原版Util.growByHalf
    public static int GrowByHalf(int currentSize, int minimalNewSize)
        => (int)Math.Max(Math.Min((long)currentSize + (currentSize >> 1), 2147483639L), minimalNewSize);

    //LazyMapDictionary惰性映射字典对应原版Guava Maps.transformValues
    //按需调用valueMapper避免预先计算所有值
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
