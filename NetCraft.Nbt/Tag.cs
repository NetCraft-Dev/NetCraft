namespace NetCraft.Nbt;

//Root interface for NBT tags. Mirrors vanilla net.minecraft.nbt.Tag.
//NBT (Named Binary Tag) is Minecraft's binary serialization format, used for saves, chunks, player data and more.
public interface Tag
{
    //Object header overhead (8 bytes: the object header).
    public const int ObjectHeader = 8;

    //Array header overhead (12 bytes: object header + length int).
    public const int ArrayHeader = 12;

    //Object reference overhead (4 bytes: a compressed pointer).
    public const int ObjectReference = 4;

    //String overhead estimate (28 bytes: the String object + char[]).
    public const int StringSize = 28;

    // ============ Tag ID constants (byte-compatible with vanilla) ============

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

    //NBT nesting depth limit (keeps malicious saves from causing OOM).
    public const int MaxDepth = 512;

    //Write to the binary output. Byte-compatible with vanilla write(DataOutput).
    void Write(INbtWriter output);

    //Returns this Tag as a string (SNBT format).
    string ToString();

    //Returns the Tag ID (0-12).
    byte Id { get; }

    //Returns the type description of this Tag.
    TagType Type { get; }

    //Deep copy.
    Tag Copy();

    //Estimate the bytes this Tag occupies (used by NbtAccounter).
    int SizeInBytes();

    //Accepts a TagVisitor.
    void Accept(TagVisitor visitor);

    //Accepts a streaming StreamTagVisitor.
    StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor);

    //Visit as the root entry (mirrors vanilla acceptAsRoot).
    void AcceptAsRoot(StreamTagVisitor output)
    {
        var entryResult = output.VisitRootEntry(Type);
        if (entryResult == StreamTagVisitor.ValueResult.Continue)
        {
            Accept(output);
        }
    }

    //Try to return this as a string (only StringTag overrides it).
    virtual string? AsString() => null;

    //Try to return this as a number (only NumericTag subclasses override it).
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

