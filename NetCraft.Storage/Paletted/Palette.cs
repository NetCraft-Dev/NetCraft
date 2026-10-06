namespace NetCraft.Storage.Paletted;

//Palette interface, maps to vanilla net.minecraft.world.level.chunk.Palette
//Manages the mapping between storage int ids and T values
public interface Palette<T>
{
    //Return the id for value; if absent, grow via resizeHandler, maps to vanilla idFor
    int IdFor(T value, PaletteResize<T> resizeHandler);

    //Whether a value satisfying predicate exists, maps to vanilla maybeHas
    bool MaybeHas(Predicate<T> predicate);

    //Get the value for an id, maps to vanilla valueFor
    T ValueFor(int index);

    //Actual entry count in the palette, maps to vanilla getSize
    int Size { get; }

    Palette<T> Copy();
}
