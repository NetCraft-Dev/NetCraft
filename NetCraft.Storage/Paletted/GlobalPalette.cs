using NetCraft.Registry;

namespace NetCraft.Storage.Paletted;

//GlobalPalette, maps to vanilla net.minecraft.world.level.chunk.GlobalPalette
//Uses IdMap directly with no palette mapping, for when the palette is full
public sealed class GlobalPalette<T> : Palette<T>
{
    private readonly IdMap<T> _registry;

    public GlobalPalette(IdMap<T> registry) { _registry = registry; }

    public int Size => _registry.Size;

    //value is looked up in the registry directly; a missing id returns 0, maps to vanilla idFor
    public int IdFor(T value, PaletteResize<T> resizeHandler)
    {
        var id = _registry.GetId(value);
        return id == -1 ? 0 : id;
    }

    public bool MaybeHas(Predicate<T> predicate) => true;

    //index is looked up in the registry by id; a missing entry throws MissingPaletteEntryException
    public T ValueFor(int index)
    {
        var value = _registry.ById(index);
        return value is null ? throw new MissingPaletteEntryException(index) : value;
    }

    public Palette<T> Copy() => this;
}
