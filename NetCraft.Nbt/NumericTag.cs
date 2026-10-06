using System.Buffers.Binary;
using System.Globalization;

namespace NetCraft.Nbt;

//EndTag (TAG_End, ID=0). Mirrors vanilla net.minecraft.nbt.EndTag.
//Marks the end of a CompoundTag or ListTag. Empty implementation, a singleton.
public sealed class EndTag : Tag
{
    public static readonly EndTag Instance = new();

    private EndTag() { }

    public byte Id => Tag.TagEnd;

    public TagType Type => EndTagType.Instance;

    public void Write(INbtWriter output) { /* EndTag writes no data */ }

    public override string ToString() => "END";

    public Tag Copy() => Instance;

    public int SizeInBytes() => 0;

    public void Accept(TagVisitor visitor) => visitor.VisitEnd(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitEnd();

    public void AcceptAsRoot(StreamTagVisitor output)
    {
        if (output.VisitRootEntry(Type) == StreamTagVisitor.ValueResult.Continue)
        {
            Accept(output);
        }
    }

    public sealed class EndTagType : TagType
    {
        public static readonly EndTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter) => EndTag.Instance;

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
            => output.VisitEnd();

        public void Skip(INbtReader input, int count, NbtAccounter accounter) { }

        public void Skip(INbtReader input, NbtAccounter accounter) { }

        public string Name => "TAG_End";
        public string PrettyName => "TAG_End";
    }
}

//<summary>Base class for numeric Tags. Mirrors vanilla NumericTag.
//Does not implement the Tag interface directly (to avoid carrying abstract methods);
//subclasses use : NumericTag, Tag to inherit the base class and implement the interface in one go.
public abstract class NumericTag
{
    //Returns this Tag's numeric value. Subclasses must implement it.
    public abstract Number? AsNumber();
}

//ByteTag (TAG_Byte, ID=1). Mirrors vanilla net.minecraft.nbt.ByteTag.
//Stores a 1-byte signed integer. Immutable, Copy returns itself.
public sealed class ByteTag(byte value) : NumericTag, Tag
{
    public byte Value { get; } = value;

    public byte Id => Tag.TagByte;

    public TagType Type => ByteTagType.Instance;

    private static readonly ByteTag[] _cache = BuildCache();

    private static ByteTag[] BuildCache()
    {
        var cache = new ByteTag[256];
        for (var i = 0; i < cache.Length; i++)
            cache[i] = new ByteTag((byte)i);
        return cache;
    }

    //Singleton for 0 (equivalent to ValueOf(0)).
    public static readonly ByteTag Zero = ValueOf((byte)0);

    //Singleton for 1 (equivalent to ValueOf(1)).
    public static readonly ByteTag One = ValueOf((byte)1);

    //Get a cached ByteTag instance. Mirrors vanilla ByteTag.valueOf(byte).
    public static ByteTag ValueOf(byte value) => _cache[value];

    //Get the 0/1 ByteTag. Mirrors vanilla ByteTag.valueOf(boolean).
    public static ByteTag ValueOf(bool value) => value ? One : Zero;

    public void Write(INbtWriter output) => output.WriteByte(Value);

    public override string ToString() => Value + "b";

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + 1;

    public void Accept(TagVisitor visitor) => visitor.VisitByte(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitByte(Value);

    public override Number? AsNumber() => Value;

    public override bool Equals(object? obj) => obj is ByteTag b && b.Value == Value;
    public override int GetHashCode() => Value;

    public sealed class ByteTagType : TagType.StaticSize
    {
        public static readonly ByteTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.AccountBytes(1);
            return ValueOf(input.ReadByte());
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(1);
            return output.VisitByte(input.ReadByte());
        }

        public int Size => 1;
        public string Name => "TAG_Byte";
        public string PrettyName => "TAG_Byte";
    }
}

//ShortTag (TAG_Short, ID=2). Mirrors vanilla net.minecraft.nbt.ShortTag.
//Stores a 2-byte big-endian signed integer.
public sealed class ShortTag(short value) : NumericTag, Tag
{
    public short Value { get; } = value;

    public byte Id => Tag.TagShort;
    public TagType Type => ShortTagType.Instance;

    //Factory method. Mirrors vanilla ShortTag.valueOf(short).
    public static ShortTag ValueOf(short value) => new(value);

    public void Write(INbtWriter output) => output.WriteShort(Value);

    public override string ToString() => Value + "s";

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + 2;

    public void Accept(TagVisitor visitor) => visitor.VisitShort(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitShort(Value);

    public override Number? AsNumber() => Value;

    public override bool Equals(object? obj) => obj is ShortTag s && s.Value == Value;
    public override int GetHashCode() => Value;

    public sealed class ShortTagType : TagType.StaticSize
    {
        public static readonly ShortTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.AccountBytes(2);
            return ValueOf(input.ReadShort());
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(2);
            return output.VisitShort(input.ReadShort());
        }

        public int Size => 2;
        public string Name => "TAG_Short";
        public string PrettyName => "TAG_Short";
    }
}

//IntTag (TAG_Int, ID=3). Mirrors vanilla net.minecraft.nbt.IntTag.
//Stores a 4-byte big-endian signed integer.
public sealed class IntTag(int value) : NumericTag, Tag
{
    public int Value { get; } = value;

    public byte Id => Tag.TagInt;
    public TagType Type => IntTagType.Instance;

    //Factory method. Mirrors vanilla IntTag.valueOf(int).
    public static IntTag ValueOf(int value) => new(value);

    public void Write(INbtWriter output) => output.WriteInt(Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + 4;

    public void Accept(TagVisitor visitor) => visitor.VisitInt(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitInt(Value);

    public override Number? AsNumber() => Value;

    public override bool Equals(object? obj) => obj is IntTag i && i.Value == Value;
    public override int GetHashCode() => Value;

    public sealed class IntTagType : TagType.StaticSize
    {
        public static readonly IntTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.AccountBytes(4);
            return ValueOf(input.ReadInt());
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(4);
            return output.VisitInt(input.ReadInt());
        }

        public int Size => 4;
        public string Name => "TAG_Int";
        public string PrettyName => "TAG_Int";
    }
}

//LongTag (TAG_Long, ID=4). Mirrors vanilla net.minecraft.nbt.LongTag.
//Stores an 8-byte big-endian signed integer.
public sealed class LongTag(long value) : NumericTag, Tag
{
    public long Value { get; } = value;

    public byte Id => Tag.TagLong;
    public TagType Type => LongTagType.Instance;

    //Factory method. Mirrors vanilla LongTag.valueOf(long).
    public static LongTag ValueOf(long value) => new(value);

    public void Write(INbtWriter output) => output.WriteLong(Value);

    public override string ToString() => Value + "L";

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + 8;

    public void Accept(TagVisitor visitor) => visitor.VisitLong(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitLong(Value);

    public override Number? AsNumber() => Value;

    public override bool Equals(object? obj) => obj is LongTag l && l.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();

    public sealed class LongTagType : TagType.StaticSize
    {
        public static readonly LongTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.AccountBytes(8);
            return ValueOf(input.ReadLong());
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(8);
            return output.VisitLong(input.ReadLong());
        }

        public int Size => 8;
        public string Name => "TAG_Long";
        public string PrettyName => "TAG_Long";
    }
}

//FloatTag (TAG_Float, ID=5). Mirrors vanilla net.minecraft.nbt.FloatTag.
//Stores a 4-byte big-endian IEEE 754 single-precision float.
public sealed class FloatTag(float value) : NumericTag, Tag
{
    public float Value { get; } = value;

    public byte Id => Tag.TagFloat;
    public TagType Type => FloatTagType.Instance;

    //Factory method. Mirrors vanilla FloatTag.valueOf(float).
    public static FloatTag ValueOf(float value) => new(value);

    public void Write(INbtWriter output) => output.WriteFloat(Value);

    public override string ToString() => Value + "f";

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + 4;

    public void Accept(TagVisitor visitor) => visitor.VisitFloat(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitFloat(Value);

    public override Number? AsNumber() => Value;

    public override bool Equals(object? obj) => obj is FloatTag f && BitConverter.SingleToInt32Bits(f.Value) == BitConverter.SingleToInt32Bits(Value);
    public override int GetHashCode() => BitConverter.SingleToInt32Bits(Value);

    public sealed class FloatTagType : TagType.StaticSize
    {
        public static readonly FloatTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.AccountBytes(4);
            return ValueOf(input.ReadFloat());
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(4);
            return output.VisitFloat(input.ReadFloat());
        }

        public int Size => 4;
        public string Name => "TAG_Float";
        public string PrettyName => "TAG_Float";
    }
}

//DoubleTag (TAG_Double, ID=6). Mirrors vanilla net.minecraft.nbt.DoubleTag.
//Stores an 8-byte big-endian IEEE 754 double-precision float.
public sealed class DoubleTag(double value) : NumericTag, Tag
{
    public double Value { get; } = value;

    public byte Id => Tag.TagDouble;
    public TagType Type => DoubleTagType.Instance;

    //Factory method. Mirrors vanilla DoubleTag.valueOf(double).
    public static DoubleTag ValueOf(double value) => new(value);

    public void Write(INbtWriter output) => output.WriteDouble(Value);

    public override string ToString() => Value + "d";

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + 8;

    public void Accept(TagVisitor visitor) => visitor.VisitDouble(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitDouble(Value);

    public override Number? AsNumber() => Value;

    public override bool Equals(object? obj) => obj is DoubleTag d && BitConverter.DoubleToInt64Bits(d.Value) == BitConverter.DoubleToInt64Bits(Value);
    public override int GetHashCode() => BitConverter.DoubleToInt64Bits(Value).GetHashCode();

    public sealed class DoubleTagType : TagType.StaticSize
    {
        public static readonly DoubleTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            accounter.AccountBytes(8);
            return ValueOf(BitConverter.Int64BitsToDouble(input.ReadDoubleBits()));
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            accounter.AccountBytes(8);
            return output.VisitDouble(BitConverter.Int64BitsToDouble(input.ReadDoubleBits()));
        }

        public int Size => 8;
        public string Name => "TAG_Double";
        public string PrettyName => "TAG_Double";
    }
}

