namespace NetCraft.Storage.Paletted;

//线性palette对应原版LinearPalette
//用数组顺序存值按索引查找适合条目少的场景
public sealed class LinearPalette<T> : Palette<T>
{
    private readonly T?[] _values;
    private readonly int _bits;
    private int _size;

    public LinearPalette(int bits, IReadOnlyList<T> paletteEntries)
    {
        _values = new T[1 << bits];
        _bits = bits;
        if (paletteEntries.Count > _values.Length)
            throw new ArgumentException($"Can't initialize LinearPalette of size {_values.Length} with {paletteEntries.Count} entries");
        for (var i = 0; i < paletteEntries.Count; i++)
            _values[i] = paletteEntries[i];
        _size = paletteEntries.Count;
    }

    private LinearPalette(T?[] values, int bits, int size)
    {
        _values = values;
        _bits = bits;
        _size = size;
    }

    //顺序查找到返回索引未找到且空间足够则追加否则扩容对应原版idFor
    public int IdFor(T value, PaletteResize<T> resizeHandler)
    {
        for (var i = 0; i < _size; i++)
        {
            if (EqualityComparer<T>.Default.Equals(_values[i], value)) return i;
        }
        var index = _size;
        if (index < _values.Length)
        {
            _values[index] = value;
            _size++;
            return index;
        }
        return resizeHandler.OnResize(_bits + 1, value);
    }

    public bool MaybeHas(Predicate<T> predicate)
    {
        for (var i = 0; i < _size; i++)
        {
            if (predicate(_values[i]!)) return true;
        }
        return false;
    }

    public T ValueFor(int index)
    {
        if (index < 0 || index >= _size) throw new MissingPaletteEntryException(index);
        return _values[index]!;
    }

    public int Size => _size;

    public Palette<T> Copy() => new LinearPalette<T>((T?[])_values.Clone(), _bits, _size);
}
