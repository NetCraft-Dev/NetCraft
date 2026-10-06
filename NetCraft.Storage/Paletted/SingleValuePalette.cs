namespace NetCraft.Storage.Paletted;

//Single-value palette, maps to vanilla SingleValuePalette
//Stores a single value, for the common case where all storage uses the same value
//C# unconstrained generic T? does not produce Nullable<T> for struct T, so a _hasValue flag replaces the _value is null check
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

    //Return 0 when a value is stored or none is stored yet, otherwise trigger a resize, maps to vanilla idFor
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
