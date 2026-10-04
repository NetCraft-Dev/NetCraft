using NetCraft.Registry;

namespace NetCraft.Storage.Paletted;

//GlobalPalette对应原版net.minecraft.world.level.chunk.GlobalPalette
//直接用IdMap不做palette映射适合palette满的情况
public sealed class GlobalPalette<T> : Palette<T>
{
    private readonly IdMap<T> _registry;

    public GlobalPalette(IdMap<T> registry) { _registry = registry; }

    public int Size => _registry.Size;

    //value直接查registry id不存在返回0对应原版idFor
    public int IdFor(T value, PaletteResize<T> resizeHandler)
    {
        var id = _registry.GetId(value);
        return id == -1 ? 0 : id;
    }

    public bool MaybeHas(Predicate<T> predicate) => true;

    //index直接查registry byId不存在抛MissingPaletteEntryException
    public T ValueFor(int index)
    {
        var value = _registry.ById(index);
        return value is null ? throw new MissingPaletteEntryException(index) : value;
    }

    public Palette<T> Copy() => this;
}
