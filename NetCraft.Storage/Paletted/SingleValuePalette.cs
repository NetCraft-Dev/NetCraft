namespace NetCraft.Storage.Paletted;

//单值palette对应原版SingleValuePalette
//只存一个值适合所有storage都用同一个值的常见情况
//C#未约束泛型T?对struct T不生成Nullable<T>故用_hasValue标记替代_value is null判断
public sealed class SingleValuePalette<T> : Palette<T>
{
    private bool _hasValue;
    private T _value = default!;

    public SingleValuePalette(IReadOnlyList<T> paletteEntries)
    {
        if (paletteEntries.Count > 0)
        {
            if (paletteEntries.Count > 1)
                throw new ArgumentException($"Can't initialize SingleValuePalette with {paletteEntries.Count} values.");
            _value = paletteEntries[0];
            _hasValue = true;
        }
    }

    //已存值或还没值时直接返回0否则触发扩容对应原版idFor
    public int IdFor(T value, PaletteResize<T> resizeHandler)
    {
        if (!_hasValue || EqualityComparer<T>.Default.Equals(_value, value))
        {
            _hasValue = true;
            _value = value;
            return 0;
        }
        return resizeHandler.OnResize(1, value);
    }

    public bool MaybeHas(Predicate<T> predicate)
    {
        if (!_hasValue) throw new InvalidOperationException("Use of an uninitialized palette");
        return predicate(_value);
    }

    public T ValueFor(int index)
    {
        if (!_hasValue || index != 0)
            throw new MissingPaletteEntryException(index);
        return _value;
    }

    public int Size => 1;

    public Palette<T> Copy()
    {
        if (!_hasValue) throw new InvalidOperationException("Use of an uninitialized palette");
        return this;
    }
}
