namespace NetCraft.Storage.Paletted;

//Palette resize callback, maps to vanilla net.minecraft.world.level.chunk.PaletteResize
//Called by PalettedContainer when the palette is out of space, returns the id in the new palette
public interface PaletteResize<T>
{
    int OnResize(int bits, T lastAddedValue);

    //Throws for scenarios where resizing is unexpected, maps to vanilla noResizeExpected
    static PaletteResize<T> NoResizeExpected()
        => new NoResizePaletteResize<T>();
}

//Throws when resizing is unexpected, maps to the vanilla noResizeExpected lambda
internal sealed class NoResizePaletteResize<T> : PaletteResize<T>
{
    public int OnResize(int bits, T lastAddedValue)
        => throw new InvalidOperationException($"Unexpected palette resize, bits = {bits}, added value = {lastAddedValue}");
}

//Palette entry missing exception, maps to vanilla MissingPaletteEntryException
public sealed class MissingPaletteEntryException : Exception
{
    public MissingPaletteEntryException(int index) : base($"Missing Palette entry for index {index}.") { }
}
