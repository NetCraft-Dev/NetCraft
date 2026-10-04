using System.Buffers.Binary;
using System.Text;

namespace NetCraft.Nbt;

//StringTag（TAG_String，ID=8）。对应原版 net.minecraft.nbt.StringTag。
//存储 Java modified UTF-8 字符串，2 字节长度前缀。
public sealed class StringTag(string value) : Tag
{
    public string Value { get; } = value;

    public byte Id => Tag.TagString;
    public TagType Type => StringTagType.Instance;

    //空字符串单例。
    private static readonly StringTag Empty = new("");

    //工厂方法。对应原版 StringTag.valueOf(String)：空串返回单例，否则新建。
    public static StringTag ValueOf(string data)
        => string.IsNullOrEmpty(data) ? Empty : new StringTag(data);

    public void Write(INbtWriter output) => output.WriteUtf(Value);

    public override string ToString()
    {
        // SNBT 格式：用引号包裹，转义特殊字符
        var sb = new StringBuilder();
        sb.Append('"');
        foreach (var c in Value)
        {
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c,
            });
        }
        sb.Append('"');
        return sb.ToString();
    }

    public Tag Copy() => this;

    public int SizeInBytes() => Tag.ObjectHeader + Tag.StringSize + Value.Length * 2;

    public void Accept(TagVisitor visitor) => visitor.VisitString(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitString(Value);

    public new string? AsString() => Value;

    public override bool Equals(object? obj) => obj is StringTag s && s.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();

    public sealed class StringTagType : TagType.VariableSize
    {
        public static readonly StringTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            var value = input.ReadUtf();
            accounter.AccountBytes(Tag.StringSize + value.Length * 2L);
            return ValueOf(value);
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            var value = input.ReadUtf();
            accounter.AccountBytes(Tag.StringSize + value.Length * 2L);
            return output.VisitString(value);
        }

        public void Skip(INbtReader input, NbtAccounter accounter)
        {
            var len = (ushort)input.ReadShort();
            accounter.AccountBytes(Tag.StringSize + len * 2L);
            input.SkipBytes(len);
        }

        public string Name => "TAG_String";
        public string PrettyName => "TAG_String";
    }

    //跳过字符串（不构造对象，仅移动读取位置）。
    public static void SkipString(INbtReader input)
    {
        var len = (ushort)input.ReadShort();
        input.SkipBytes(len);
    }
}

//ByteArrayTag（TAG_Byte_Array，ID=7）。对应原版 net.minecraft.nbt.ByteArrayTag。
//存储 4 字节长度前缀 + 长度个 byte。
public sealed class ByteArrayTag(byte[] value) : Tag
{
    public byte[] Value { get; } = value;
    public int Length => Value.Length;

    public byte Id => Tag.TagByteArray;
    public TagType Type => ByteArrayTagType.Instance;

    public void Write(INbtWriter output)
    {
        output.WriteInt(Value.Length);
        output.WriteBytes(Value);
    }

    public override string ToString()
    {
        return "[" + string.Join(", ", Value) + "]";
    }

    public Tag Copy() => new ByteArrayTag((byte[])Value.Clone());

    public int SizeInBytes() => Tag.ArrayHeader + Value.Length;

    public void Accept(TagVisitor visitor) => visitor.VisitByteArray(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitByteArray(Value);

    public new byte[]? AsByteArray() => Value;

    public override bool Equals(object? obj) => obj is ByteArrayTag b && b.Value.AsSpan().SequenceEqual(Value);
    public override int GetHashCode()
    {
        var h = Value.Length;
        if (Value.Length > 0) h = (h * 31) ^ Value[0];
        return h;
    }

    public sealed class ByteArrayTagType : TagType.VariableSize
    {
        public static readonly ByteArrayTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len);
            var bytes = new byte[len];
            input.ReadBytes(bytes);
            return new ByteArrayTag(bytes);
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len);
            var bytes = new byte[len];
            input.ReadBytes(bytes);
            return output.VisitByteArray(bytes);
        }

        public void Skip(INbtReader input, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len);
            input.SkipBytes(len);
        }

        public string Name => "TAG_Byte_Array";
        public string PrettyName => "TAG_Byte_Array";
    }
}

//IntArrayTag（TAG_Int_Array，ID=11）。对应原版 net.minecraft.nbt.IntArrayTag。
//存储 4 字节长度前缀 + 长度个 int（大端）。
public sealed class IntArrayTag(int[] value) : Tag
{
    public int[] Value { get; } = value;
    public int Length => Value.Length;

    public byte Id => Tag.TagIntArray;
    public TagType Type => IntArrayTagType.Instance;

    public void Write(INbtWriter output)
    {
        output.WriteInt(Value.Length);
        foreach (var v in Value)
            output.WriteInt(v);
    }

    public override string ToString()
    {
        return "[I; " + string.Join(", ", Value) + "]";
    }

    public Tag Copy() => new IntArrayTag((int[])Value.Clone());

    public int SizeInBytes() => Tag.ArrayHeader + Value.Length * 4;

    public void Accept(TagVisitor visitor) => visitor.VisitIntArray(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitIntArray(Value);

    public new int[]? AsIntArray() => Value;

    public override bool Equals(object? obj) => obj is IntArrayTag i && i.Value.AsSpan().SequenceEqual(Value);
    public override int GetHashCode()
    {
        var h = Value.Length;
        if (Value.Length > 0) h = (h * 31) ^ Value[0];
        return h;
    }

    public sealed class IntArrayTagType : TagType.VariableSize
    {
        public static readonly IntArrayTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len * 4L);
            var arr = new int[len];
            for (var i = 0; i < len; i++)
                arr[i] = input.ReadInt();
            return new IntArrayTag(arr);
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len * 4L);
            var arr = new int[len];
            for (var i = 0; i < len; i++)
                arr[i] = input.ReadInt();
            return output.VisitIntArray(arr);
        }

        public void Skip(INbtReader input, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len * 4L);
            input.SkipBytes(len * 4);
        }

        public string Name => "TAG_Int_Array";
        public string PrettyName => "TAG_Int_Array";
    }
}

//LongArrayTag（TAG_Long_Array，ID=12）。对应原版 net.minecraft.nbt.LongArrayTag。
//存储 4 字节长度前缀 + 长度个 long（大端）。
public sealed class LongArrayTag(long[] value) : Tag
{
    public long[] Value { get; } = value;
    public int Length => Value.Length;

    public byte Id => Tag.TagLongArray;
    public TagType Type => LongArrayTagType.Instance;

    public void Write(INbtWriter output)
    {
        output.WriteInt(Value.Length);
        foreach (var v in Value)
            output.WriteLong(v);
    }

    public override string ToString()
    {
        return "[L; " + string.Join(", ", Value) + "]";
    }

    public Tag Copy() => new LongArrayTag((long[])Value.Clone());

    public int SizeInBytes() => Tag.ArrayHeader + Value.Length * 8;

    public void Accept(TagVisitor visitor) => visitor.VisitLongArray(this);

    public StreamTagVisitor.ValueResult Accept(StreamTagVisitor visitor) => visitor.VisitLongArray(Value);

    public new long[]? AsLongArray() => Value;

    public override bool Equals(object? obj) => obj is LongArrayTag l && l.Value.AsSpan().SequenceEqual(Value);
    public override int GetHashCode()
    {
        var h = Value.Length;
        if (Value.Length > 0) h = (h * 31) ^ Value[0].GetHashCode();
        return h;
    }

    public sealed class LongArrayTagType : TagType.VariableSize
    {
        public static readonly LongArrayTagType Instance = new();

        public Tag Load(INbtReader input, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len * 8L);
            var arr = new long[len];
            for (var i = 0; i < len; i++)
                arr[i] = input.ReadLong();
            return new LongArrayTag(arr);
        }

        public StreamTagVisitor.ValueResult Parse(INbtReader input, StreamTagVisitor output, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len * 8L);
            var arr = new long[len];
            for (var i = 0; i < len; i++)
                arr[i] = input.ReadLong();
            return output.VisitLongArray(arr);
        }

        public void Skip(INbtReader input, NbtAccounter accounter)
        {
            var len = input.ReadInt();
            accounter.AccountBytes(Tag.ArrayHeader + len * 8L);
            input.SkipBytes(len * 8);
        }

        public string Name => "TAG_Long_Array";
        public string PrettyName => "TAG_Long_Array";
    }
}

