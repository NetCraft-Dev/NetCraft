using System.Buffers.Binary;
using System.Text;

namespace NetCraft.Nbt;

//NBT binary reader abstraction. Mirrors vanilla java.io.DataInput.
//NBT uses big-endian byte order; every method here reads and writes big-endian.
//C# optimization: ReadOnlySpan{Byte} + BinaryPrimitives replace Java's byte-by-byte reads.
public interface INbtReader
{
    //Reads 1 byte.
    byte ReadByte();

    //Reads a 2-byte big-endian short.
    short ReadShort();

    //Reads a 4-byte big-endian int.
    int ReadInt();

    //Reads an 8-byte big-endian long.
    long ReadLong();

    //Reads a 4-byte big-endian float.
    float ReadFloat();

    //Reads an 8-byte big-endian double.
    long ReadDoubleBits();

    //Reads a UTF string (Java modified UTF-8).
    string ReadUtf();

    //Skips n bytes.
    void SkipBytes(int n);

    //Reads the remaining bytes into buffer.
    void ReadBytes(Span<byte> buffer);
}

//NBT binary writer abstraction. Mirrors vanilla java.io.DataOutput.
//NBT uses big-endian byte order.
public interface INbtWriter
{
    void WriteByte(byte v);
    void WriteShort(short v);
    void WriteInt(int v);
    void WriteLong(long v);
    void WriteFloat(float v);
    void WriteDouble(double v);

    //Writes a Java modified UTF-8 string (2-byte length prefix + modified UTF-8 payload).
    void WriteUtf(string s);

    void WriteBytes(ReadOnlySpan<byte> buffer);
}

//NBT reader implementation backed by BinaryReader.
//Big-endian byte order.
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
        // Java modified UTF-8: 2-byte length prefix (unsigned short) + modified UTF-8 payload
        var length = (ushort)ReadShort();
        Span<byte> bytes = length <= 256 ? stackalloc byte[length] : new byte[length];
        // Uses ReadBytes instead of Read: a stream may return partial chunks, which would silently truncate long strings
        ReadBytes(bytes);
        return ModifiedUtf8Decoder.Decode(bytes);
    }

    public void SkipBytes(int n)
    {
        // BaseStream.Seek is not an option: streams without seek support such as GZipStream throw NotSupportedException
        // Actually reads and discards n bytes, which works for every stream
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
        // A single Stream.Read need not fill the buffer; streams like GZipStream return chunks, so loop until it is full
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

//NBT writer implementation backed by BinaryWriter.
//Big-endian byte order.
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
        // Java modified UTF-8 encoding
        var bytes = ModifiedUtf8Encoder.Encode(s);
        WriteShort((short)bytes.Length);
        _writer.Write(bytes);
    }

    public void WriteBytes(ReadOnlySpan<byte> buffer) => _writer.Write(buffer);
}

//Java modified UTF-8 decoder.
//Differences from standard UTF-8:
//- the null character (U+0000) is encoded as 2 bytes (0xC0 0x80)
//- supplementary plane characters are encoded as surrogate pairs (CESU-8)
internal static class ModifiedUtf8Decoder
{
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        // Mostly ASCII, so a fast path
        var hasNonAscii = false;
        foreach (var b in bytes)
        {
            if (b >= 0x80) { hasNonAscii = true; break; }
        }
        if (!hasNonAscii)
        {
            return Encoding.ASCII.GetString(bytes);
        }

        // Slow path: modified UTF-8 decoding
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
                // CESU-8 surrogate pair handling
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
        // Estimate the maximum length: at most 3 bytes per char (modified UTF-8)
        var bytes = new byte[s.Length * 3];
        var pos = 0;
        foreach (var c in s)
        {
            if (c == 0)
            {
                // null is encoded as 0xC0 0x80
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
                // CESU-8 surrogate pairs: encoded as-is into two 3-byte sequences
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

