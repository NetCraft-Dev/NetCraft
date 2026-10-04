using System.Buffers.Binary;
using System.Globalization;

namespace NetCraft.Nbt;

//EndTag（TAG_End，ID=0）。对应原版 net.minecraft.nbt.EndTag。
//标记 CompoundTag 或 ListTag 的结束。空实现，单例。
public sealed class EndTag : Tag
{
    public static readonly EndTag Instance = new();

    private EndTag() { }

    public byte Id => Tag.TagEnd;

    public TagType Type => EndTagType.Instance;

    public void Write(INbtWriter output) { /* EndTag 不写入任何数据 */ }

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

//<summary>数值 Tag 基类。对应原版 NumericTag。
//不直接实现 Tag 接口（避免 abstract 方法传递负担），
//子类通过 : NumericTag, Tag 同时继承基类和实现接口。
public abstract class NumericTag
{
    //返回此 Tag 的数值。子类必须实现。
    public abstract Number? AsNumber();
}

//ByteTag（TAG_Byte，ID=1）。对应原版 net.minecraft.nbt.ByteTag。
//存储 1 字节有符号整数。不可变，Copy 返回自身。
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

    //0 值单例（等价 ValueOf(0)）。
    public static readonly ByteTag Zero = ValueOf((byte)0);

    //1 值单例（等价 ValueOf(1)）。
    public static readonly ByteTag One = ValueOf((byte)1);

    //获取缓存的 ByteTag 实例。对应原版 ByteTag.valueOf(byte)。
    public static ByteTag ValueOf(byte value) => _cache[value];

    //获取 0/1 表示的 ByteTag。对应原版 ByteTag.valueOf(boolean)。
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

//ShortTag（TAG_Short，ID=2）。对应原版 net.minecraft.nbt.ShortTag。
//存储 2 字节大端有符号整数。
public sealed class ShortTag(short value) : NumericTag, Tag
{
    public short Value { get; } = value;

    public byte Id => Tag.TagShort;
    public TagType Type => ShortTagType.Instance;

    //工厂方法。对应原版 ShortTag.valueOf(short)。
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

//IntTag（TAG_Int，ID=3）。对应原版 net.minecraft.nbt.IntTag。
//存储 4 字节大端有符号整数。
public sealed class IntTag(int value) : NumericTag, Tag
{
    public int Value { get; } = value;

    public byte Id => Tag.TagInt;
    public TagType Type => IntTagType.Instance;

    //工厂方法。对应原版 IntTag.valueOf(int)。
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

//LongTag（TAG_Long，ID=4）。对应原版 net.minecraft.nbt.LongTag。
//存储 8 字节大端有符号整数。
public sealed class LongTag(long value) : NumericTag, Tag
{
    public long Value { get; } = value;

    public byte Id => Tag.TagLong;
    public TagType Type => LongTagType.Instance;

    //工厂方法。对应原版 LongTag.valueOf(long)。
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

//FloatTag（TAG_Float，ID=5）。对应原版 net.minecraft.nbt.FloatTag。
//存储 4 字节大端 IEEE 754 单精度浮点。
public sealed class FloatTag(float value) : NumericTag, Tag
{
    public float Value { get; } = value;

    public byte Id => Tag.TagFloat;
    public TagType Type => FloatTagType.Instance;

    //工厂方法。对应原版 FloatTag.valueOf(float)。
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

//DoubleTag（TAG_Double，ID=6）。对应原版 net.minecraft.nbt.DoubleTag。
//存储 8 字节大端 IEEE 754 双精度浮点。
public sealed class DoubleTag(double value) : NumericTag, Tag
{
    public double Value { get; } = value;

    public byte Id => Tag.TagDouble;
    public TagType Type => DoubleTagType.Instance;

    //工厂方法。对应原版 DoubleTag.valueOf(double)。
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

