namespace NetCraft.Nbt;

//NBT 标签根接口。对应原版 net.minecraft.nbt.Tag。
//NBT（Named Binary Tag）是 Minecraft 的二进制序列化格式，用于存档、区块、玩家数据等。
public interface Tag
{
    //对象头开销（8 字节：对象头）。
    public const int ObjectHeader = 8;

    //数组头开销（12 字节：对象头 + 长度 int）。
    public const int ArrayHeader = 12;

    //对象引用开销（4 字节：指针压缩）。
    public const int ObjectReference = 4;

    //字符串开销估算（28 字节：String 对象 + char[]）。
    public const int StringSize = 28;

    // ============ Tag ID 常量（字节级兼容原版） ============

    public const byte TagEnd = 0;
    public const byte TagByte = 1;
    public const byte TagShort = 2;
    public const byte TagInt = 3;
    public const byte TagLong = 4;
    public const byte TagFloat = 5;
    public const byte TagDouble = 6;
    public const byte TagByteArray = 7;
    public const byte TagString = 8;
    public const byte TagList = 9;
    public const byte TagCompound = 10;
    public const byte TagIntArray = 11;
    public const byte TagLongArray = 12;

    //NBT 嵌套深度上限（防恶意存档 OOM）。
    public const int MaxDepth = 512;

    //写入到二进制输出。字节级兼容原版 write(DataOutput)。
    void Write(INbtWriter output);

    //返回此 Tag 的字符串表示（SNBT 格式）。
    string ToString();

    //返回 Tag ID（0-12）。
    byte Id { get; }

    //返回此 Tag 的类型描述。
    TagType Type { get; }

    //深拷贝。
    Tag Copy();

    //估算此 Tag 占用字节数（用于 NbtAccounter）。
    int SizeInBytes();

    //接受 TagVisitor 访问。
    void Accept(TagVisitor visitor);

    //接受 StreamTagVisitor 流式访问。
    StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor);

    //作为根条目访问（对应原版 acceptAsRoot）。
    void AcceptAsRoot(StreamTagVisitor output)
    {
        var entryResult = output.VisitRootEntry(Type);
        if (entryResult == StreamTagVisitor.ValueResult.Continue)
        {
            Accept(output);
        }
    }

    //尝试作为字符串返回（仅 StringTag 重写）。
    virtual string? AsString() => null;

    //尝试作为数字返回（仅 NumericTag 子类重写）。
    virtual Number? AsNumber() => null;

    virtual byte? AsByte() => AsNumber()?.ByteValue();
    virtual short? AsShort() => AsNumber()?.ShortValue();
    virtual int? AsInt() => AsNumber()?.IntValue();
    virtual long? AsLong() => AsNumber()?.LongValue();
    virtual float? AsFloat() => AsNumber()?.FloatValue();
    virtual double? AsDouble() => AsNumber()?.DoubleValue();

    virtual bool? AsBoolean() => AsByte() is { } b && b != 0;

    virtual byte[]? AsByteArray() => null;
    virtual int[]? AsIntArray() => null;
    virtual long[]? AsLongArray() => null;
    virtual CompoundTag? AsCompound() => null;
    virtual ListTag? AsList() => null;
}

