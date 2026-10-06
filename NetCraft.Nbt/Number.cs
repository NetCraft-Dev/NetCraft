namespace NetCraft.Nbt;

//Numeric wrapper type. Mirrors Java's java.lang.Number.
//NumericTag uses it to return numeric values while keeping the original type information.
public readonly struct Number
{
    public double Value { get; }

    public Number(double value) => Value = value;

    public byte ByteValue() => (byte)Value;
    public short ShortValue() => (short)Value;
    public int IntValue() => (int)Value;
    public long LongValue() => (long)Value;
    public float FloatValue() => (float)Value;
    public double DoubleValue() => Value;

    public static implicit operator Number(byte v) => new(v);
    public static implicit operator Number(short v) => new(v);
    public static implicit operator Number(int v) => new(v);
    public static implicit operator Number(long v) => new(v);
    public static implicit operator Number(float v) => new(v);
    public static implicit operator Number(double v) => new(v);
}

