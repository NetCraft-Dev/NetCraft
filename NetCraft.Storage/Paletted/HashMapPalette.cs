namespace NetCraft.Storage.Paletted;

//哈希palette对应原版HashMapPalette
//用CrudeIncrementalIntIdentityHashBiMap实现值到id快速查找适合中等规模场景
public sealed class HashMapPalette<T> : Palette<T>
{
    private readonly CrudeIncrementalIntIdentityHashBiMap<T> _values;
    private readonly int _bits;

    public HashMapPalette(int bits) : this(bits, CrudeIncrementalIntIdentityHashBiMap<T>.Create<T>(1 << bits)) { }

    public HashMapPalette(int bits, IReadOnlyList<T> paletteEntries) : this(bits)
    {
        foreach (var v in paletteEntries) _values.Add(v);
    }

    private HashMapPalette(int bits, CrudeIncrementalIntIdentityHashBiMap<T> values)
    {
        _bits = bits;
        _values = values;
    }

    //已存在直接返回id不存在则加入超过容量则扩容对应原版idFor
    public int IdFor(T value, PaletteResize<T> resizeHandler)
    {
        var id = _values.GetId(value);
        if (id == -1)
        {
            id = _values.Add(value);
            if (id >= (1 << _bits)) id = resizeHandler.OnResize(_bits + 1, value);
        }
        return id;
    }

    public bool MaybeHas(Predicate<T> predicate)
    {
        for (var i = 0; i < _values.Size; i++)
        {
            if (predicate(_values.ById(i)!)) return true;
        }
        return false;
    }

    public T ValueFor(int index)
    {
        var value = _values.ById(index);
        if (value is null) throw new MissingPaletteEntryException(index);
        return value;
    }

    public int Size => _values.Size;

    public Palette<T> Copy() => new HashMapPalette<T>(_bits, _values.Copy());

    //获取所有条目对应原版getEntries用于pack时输出palette
    public IReadOnlyList<T> GetEntries()
    {
        var list = new List<T>();
        for (var i = 0; i < _values.Size; i++) list.Add(_values.ById(i)!);
        return list;
    }
}
