namespace NetCraft.Storage;

//全零位存储对应原版net.minecraft.util.ZeroBitStorage
//bits=0的特殊情况所有值都是0
public sealed class ZeroBitStorage : BitStorage
{
    public static readonly long[] Raw = Array.Empty<long>();

    public ZeroBitStorage(int size) { Size = size; }

    public int Size { get; }

    public int Bits => 0;

    public int Get(int index)
    {
        ValidateIndex(index);
        return 0;
    }

    public int GetAndSet(int index, int value)
    {
        ValidateIndex(index);
        if (value != 0) throw new ArgumentOutOfRangeException(nameof(value));
        return 0;
    }

    public void Set(int index, int value)
    {
        ValidateIndex(index);
        if (value != 0) throw new ArgumentOutOfRangeException(nameof(value));
    }

    public long[] GetRaw() => Raw;

    public void GetAll(Action<int> output)
    {
        for (var i = 0; i < Size; i++) output(0);
    }

    public void Unpack(int[] output)
    {
        Array.Fill(output, 0, 0, Size);
    }

    public BitStorage Copy() => this;

    private void ValidateIndex(int index)
    {
        if (index < 0 || index >= Size)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
