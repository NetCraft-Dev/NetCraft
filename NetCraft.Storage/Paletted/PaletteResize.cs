namespace NetCraft.Storage.Paletted;

//Palette扩容回调对应原版net.minecraft.world.level.chunk.PaletteResize
//palette空间不足时由PalettedContainer调用返回新palette中的id
public interface PaletteResize<T>
{
    int OnResize(int bits, T lastAddedValue);

    //不期望扩容场景抛异常对应原版noResizeExpected
    static PaletteResize<T> NoResizeExpected()
        => new NoResizePaletteResize<T>();
}

//扩容未期望抛异常对应原版noResizeExpected lambda
internal sealed class NoResizePaletteResize<T> : PaletteResize<T>
{
    public int OnResize(int bits, T lastAddedValue)
        => throw new InvalidOperationException($"Unexpected palette resize, bits = {bits}, added value = {lastAddedValue}");
}

//Palette条目缺失异常对应原版MissingPaletteEntryException
public sealed class MissingPaletteEntryException : Exception
{
    public MissingPaletteEntryException(int index) : base($"Missing Palette entry for index {index}.") { }
}
