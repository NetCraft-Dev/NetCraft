using System.Buffers.Binary;
using System.Text;

namespace NetCraft.Nbt;

//NBT 二进制读取器抽象。对应原版 java.io.DataInput。
//NBT 使用大端字节序（big-endian），本接口所有方法均按大端读写。
//C# 优化：用 ReadOnlySpan{Byte} + BinaryPrimitives 替代 Java 的逐字节读取。
public interface INbtReader
{
    //读取 1 字节（byte）。
    byte ReadByte();

    //读取 2 字节大端 short。
    short ReadShort();

    //读取 4 字节大端 int。
    int ReadInt();

    //读取 8 字节大端 long。
    long ReadLong();

    //读取 4 字节大端 float。
    float ReadFloat();

    //读取 8 字节大端 double。
    long ReadDoubleBits();

    //读取 UTF 字符串（Java modified UTF-8 格式）。
    string ReadUtf();

    //跳过 n 个字节。
    void SkipBytes(int n);

    //读取剩余字节到 buffer。
    void ReadBytes(Span<byte> buffer);
}

//NBT 二进制写入器抽象。对应原版 java.io.DataOutput。
//NBT 使用大端字节序。
public interface INbtWriter
{
    void WriteByte(byte v);
    void WriteShort(short v);
    void WriteInt(int v);
    void WriteLong(long v);
    void WriteFloat(float v);
    void WriteDouble(double v);

    //写入 Java modified UTF-8 字符串（2 字节长度前缀 + modified UTF-8 内容）。
    void WriteUtf(string s);

    void WriteBytes(ReadOnlySpan<byte> buffer);
}

//基于 BinaryReader 的 NBT 读取器实现。
//大端字节序。
public sealed class BinaryNbtReader(BinaryReader reader) : INbtReader
{
    private readonly BinaryReader _reader = reader;

    public byte ReadByte() => _reader.ReadByte();

    public short ReadShort() => BinaryPrimitives.ReverseEndianness(_reader.ReadInt16());

    public int ReadInt() => BinaryPrimitives.ReverseEndianness(_reader.ReadInt32());

    public long ReadLong() => BinaryPrimitives.ReverseEndianness(_reader.ReadInt64());

    public float ReadFloat() => BitConverter.Int32BitsToSingle(ReadInt());

    public long ReadDoubleBits() => ReadLong();

    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadLong());

    public string ReadUtf()
    {
        // Java modified UTF-8 格式：2 字节长度前缀（unsigned short）+ modified UTF-8 内容
        var length = (ushort)ReadShort();
        Span<byte> bytes = length <= 256 ? stackalloc byte[length] : new byte[length];
        // 走 ReadBytes 而非直接 Read 流可能分块返回 超长字符串会被静默读成残缺数据
        ReadBytes(bytes);
        return ModifiedUtf8Decoder.Decode(bytes);
    }

    public void SkipBytes(int n)
    {
        // 不能用 BaseStream.Seek：GZipStream 等不支持 Seek 的流会抛 NotSupportedException
        // 实际读取并丢弃 n 个字节，保证对所有流都可用
        Span<byte> buf = n <= 256 ? stackalloc byte[n] : new byte[n];
        var left = n;
        while (left > 0)
        {
            var read = _reader.Read(buf[..left]);
            if (read == 0) throw new EndOfStreamException($"Expected {n} bytes, got {n - left}");
            left -= read;
        }
    }

    public void ReadBytes(Span<byte> buffer)
    {
        // Stream.Read 单次不保证读满 GZipStream 一类流会分块返回 必须循环到读满为止
        var left = buffer.Length;
        while (left > 0)
        {
            var read = _reader.Read(buffer[^left..]);
            if (read == 0)
                throw new EndOfStreamException($"Expected {buffer.Length} bytes, got {buffer.Length - left}");
            left -= read;
        }
    }
}

//基于 BinaryWriter 的 NBT 写入器实现。
//大端字节序。
public sealed class BinaryNbtWriter(BinaryWriter writer) : INbtWriter
{
    private readonly BinaryWriter _writer = writer;

    public void WriteByte(byte v) => _writer.Write(v);

    public void WriteShort(short v)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf, v);
        _writer.Write(buf);
    }

    public void WriteInt(int v)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, v);
        _writer.Write(buf);
    }

    public void WriteLong(long v)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, v);
        _writer.Write(buf);
    }

    public void WriteFloat(float v)
    {
        WriteInt(BitConverter.SingleToInt32Bits(v));
    }

    public void WriteDouble(double v)
    {
        WriteLong(BitConverter.DoubleToInt64Bits(v));
    }

    public void WriteUtf(string s)
    {
        // Java modified UTF-8 编码
        var bytes = ModifiedUtf8Encoder.Encode(s);
        WriteShort((short)bytes.Length);
        _writer.Write(bytes);
    }

    public void WriteBytes(ReadOnlySpan<byte> buffer) => _writer.Write(buffer);
}

//Java modified UTF-8 解码器。
//与标准 UTF-8 的差异：
//- null 字符 (U+0000) 编码为 2 字节 (0xC0 0x80)
//- 辅助平面字符用代理对编码（CESU-8）
internal static class ModifiedUtf8Decoder
{
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        // 大部分情况下是 ASCII，快速路径
        var hasNonAscii = false;
        foreach (var b in bytes)
        {
            if (b >= 0x80) { hasNonAscii = true; break; }
        }
        if (!hasNonAscii)
        {
            return Encoding.ASCII.GetString(bytes);
        }

        // 慢路径：modified UTF-8 解码
        var sb = new StringBuilder(bytes.Length);
        var i = 0;
        while (i < bytes.Length)
        {
            var b = bytes[i++];
            if (b < 0x80)
            {
                sb.Append((char)b);
            }
            else if ((b & 0xE0) == 0xC0)
            {
                var b2 = bytes[i++];
                sb.Append((char)(((b & 0x1F) << 6) | (b2 & 0x3F)));
            }
            else if ((b & 0xF0) == 0xE0)
            {
                var b2 = bytes[i++];
                var b3 = bytes[i++];
                var cp = ((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
                // CESU-8 代理对处理
                if (cp is >= 0xD800 and <= 0xDBFF && i + 5 <= bytes.Length)
                {
                    var nextB = bytes[i++];
                    if ((nextB & 0xF0) == 0xE0)
                    {
                        var n2 = bytes[i++];
                        var n3 = bytes[i++];
                        var cp2 = ((nextB & 0x0F) << 12) | ((n2 & 0x3F) << 6) | (n3 & 0x3F);
                        var full = 0x10000 + (((cp - 0xD800) << 10) | (cp2 - 0xDC00));
                        sb.Append(char.ConvertFromUtf32(full));
                    }
                    else
                    {
                        sb.Append((char)cp);
                        i--;
                    }
                }
                else
                {
                    sb.Append((char)cp);
                }
            }
        }
        return sb.ToString();
    }
}

internal static class ModifiedUtf8Encoder
{
    public static byte[] Encode(string s)
    {
        // 估算最大长度：每个 char 最多 3 字节（modified UTF-8）
        var bytes = new byte[s.Length * 3];
        var pos = 0;
        foreach (var c in s)
        {
            if (c == 0)
            {
                // null 编码为 0xC0 0x80
                bytes[pos++] = 0xC0;
                bytes[pos++] = 0x80;
            }
            else if (c < 0x80)
            {
                bytes[pos++] = (byte)c;
            }
            else if (c < 0x800)
            {
                bytes[pos++] = (byte)(0xC0 | (c >> 6));
                bytes[pos++] = (byte)(0x80 | (c & 0x3F));
            }
            else if (char.IsSurrogate(c))
            {
                // CESU-8 代理对：保持原样编码为两个 3 字节序列
                bytes[pos++] = (byte)(0xE0 | (c >> 12));
                bytes[pos++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                bytes[pos++] = (byte)(0x80 | (c & 0x3F));
            }
            else
            {
                bytes[pos++] = (byte)(0xE0 | (c >> 12));
                bytes[pos++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                bytes[pos++] = (byte)(0x80 | (c & 0x3F));
            }
        }
        Array.Resize(ref bytes, pos);
        return bytes;
    }
}

