namespace NetCraft.Nbt;

//数值包装类型。对应 Java 的 java.lang.Number。
//NBT 的 NumericTag 用此返回数字值（保留原始类型信息）。
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

