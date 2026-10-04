namespace NetCraft.Util.Random;

//权重列表对应原版net.minecraft.util.random.WeightedList
//总权重小于64用Flat数组直接索引否则用Compact累加查找
public sealed class WeightedList<E>
{
    //FLAT_THRESHOLD小于此值用Flat策略对应原版FLAT_THRESHOLD
    private const int FlatThreshold = 64;

    private readonly int _totalWeight;
    private readonly IReadOnlyList<Weighted<E>> _items;
    private readonly Selector<E>? _selector;

    //Selector选择策略接口对应原版WeightedList.Selector
    private interface Selector<out T>
    {
        T Get(int selection);
    }

    //WeightedList构造对应原版WeightedList(List)
    //总权重零selector为null小于阈值用Flat否则用Compact
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

    //Empty空列表对应原版of
    public static WeightedList<E> Of() => new(Array.Empty<Weighted<E>>());

    //Of单值权重1对应原版of(E)
    public static WeightedList<E> Of(E value) => new(new[] { new Weighted<E>(value, 1) });

    //Of可变参数对应原版of(E...)
    public static WeightedList<E> Of(params E[] items)
    {
        var builder = Builder();
        foreach (var item in items)
            builder.Add(item);
        return builder.Build();
    }

    //Of可变Weighted参数对应原版of(Weighted...)
    public static WeightedList<E> Of(params Weighted<E>[] items) => new(items);

    //Of按List构造对应原版of(List)
    public static WeightedList<E> Of(IReadOnlyList<Weighted<E>> items)
        => new(items);

    //Builder构造器入口对应原版builder
    //嵌套类名用BuilderImpl避免与Builder()方法重名CS0102
    public static BuilderImpl<E> Builder() => new();

    //IsEmpty总权重为零即空对应原版isEmpty
    public bool IsEmpty() => _selector is null;

    //Map转换值类型保持权重对应原版map
    public WeightedList<T> Map<T>(Func<E, T> mapper)
        => new(_items.Select(e => e.Map(mapper)));

    //GetRandom按随机数选取对应原版getRandom返回Optional
    public Option<E> GetRandom(RandomSource random)
    {
        if (_selector is null)
            return Option<E>.None();
        var selection = random.NextInt(_totalWeight);
        return Option<E>.Some(_selector.Get(selection));
    }

    //GetRandomOrThrow按随机数选取对应原版getRandomOrThrow空列表抛异常
    public E GetRandomOrThrow(RandomSource random)
    {
        if (_selector is null)
            throw new InvalidOperationException("Weighted list has no elements");
        var selection = random.NextInt(_totalWeight);
        return _selector.Get(selection);
    }

    //Unwrap返回内部items对应原版unwrap
    public IReadOnlyList<Weighted<E>> Unwrap() => _items;

    //Contains值是否存在对应原版contains
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

    //FlatSelector低总权重策略对应原版WeightedList.Flat
    //按weight重复填值到Object[]直接索引O(1)
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

    //CompactSelector高总权重策略对应原版WeightedList.Compact
    //按weight累加查找空间换时间O(n)
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

    //Builder权重列表构造器对应原版WeightedList.Builder
    //类名用BuilderImpl避免与外层Builder()方法重名CS0102
    public sealed class BuilderImpl<E2>
    {
        private readonly List<Weighted<E2>> _result = new();

        //Add默认权重1对应原版add(E)
        public BuilderImpl<E2> Add(E2 item) => Add(item, 1);

        //Add指定权重对应原版add(E,int)
        public BuilderImpl<E2> Add(E2 item, int weight)
        {
            _result.Add(new Weighted<E2>(item, weight));
            return this;
        }

        //Build构造WeightedList对应原版build
        public WeightedList<E2> Build() => new(_result);
    }
}

//Option轻量Optional对应原版java.util.Optional
//random子领域内自用避免引入System.Linq 公共类型
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
