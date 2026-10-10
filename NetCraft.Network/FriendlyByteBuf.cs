using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace NetCraft.Network;

//FriendlyByteBuf protocol buffer, maps to vanilla net.minecraft.network.FriendlyByteBuf
//Wraps a MemoryStream for big-endian reads and writes, supporting VarInt and UTF-8 strings
//Not sealed so RegistryFriendlyByteBuf can inherit and add RegistryAccess
public class FriendlyByteBuf : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly bool _ownsStream;

    public FriendlyByteBuf() : this(new MemoryStream(), true) { }

    //Capacity-specified construction: a buffer built from zero doubles its way up to tens of kilobytes, and every doubling copies
    //what was written so far. Chunk packets know their approximate size up front, so they start close to it instead
    public FriendlyByteBuf(int capacity) : this(new MemoryStream(capacity), true) { }

    public FriendlyByteBuf(byte[] data) : this(new MemoryStream(data), true) { }

    public FriendlyByteBuf(MemoryStream stream, bool ownsStream)
    {
        _stream = stream;
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        _ownsStream = ownsStream;
    }

    //ReadableBytes is the number of readable bytes remaining
    public int ReadableBytes => (int)(_stream.Length - _stream.Position);

    //Length is the number of bytes written
    public int Length => (int)_stream.Length;

    //Reset clears the content but keeps the capacity, so the outbound encode buffer can be reused per connection
    //Creating a new MemoryStream and reader/writer per packet was the least worthwhile allocation on the send path
    public void Reset()
    {
        _stream.SetLength(0);
        _stream.Position = 0;
    }

    //WriteTo writes the buffer content into the target stream as-is, without an intermediate array
    //Falls back to a single copy when the underlying array is not visible, as with a buffer built from a byte[]
    public void WriteTo(Stream destination)
    {
        if (!_stream.TryGetBuffer(out var segment))
        {
            var copy = _stream.ToArray();
            destination.Write(copy, 0, copy.Length);
            return;
        }
        destination.Write(segment.Array!, segment.Offset, (int)_stream.Length);
    }

    //WriteTo appends this buffer's content into another buffer as-is, without an intermediate array
    public void WriteTo(FriendlyByteBuf target) => WriteTo(target._stream);

    //TryTakeWritten hands out the underlying array and the written length without copying, for a packet that can own the buffer outright
    //The caller must not write to this buffer afterwards, the array it now holds would change under it
    //Fails when the buffer is not backed by a visible array from offset zero, as with a buffer built from a byte[]
    public bool TryTakeWritten(out byte[] buffer, out int length)
    {
        buffer = Array.Empty<byte>();
        length = 0;
        if (!_stream.TryGetBuffer(out var segment) || segment.Offset != 0) return false;
        buffer = segment.Array!;
        length = (int)_stream.Length;
        return true;
    }

    //IsReadable indicates whether the buffer is readable
    public bool IsReadable => ReadableBytes > 0;

    //ReadBoolean reads a 1-byte boolean
    public bool ReadBoolean() => _reader.ReadBoolean();

    //ReadByte reads 1 byte
    public byte ReadByte() => _reader.ReadByte();

    //ReadShort reads a 2-byte big-endian short
    public short ReadShort() => BinaryPrimitives.ReadInt16BigEndian(_reader.ReadBytes(2));

    //ReadInt reads a 4-byte big-endian int
    public int ReadInt() => BinaryPrimitives.ReadInt32BigEndian(_reader.ReadBytes(4));

    //ReadVarInt reads a variable-length int, at most 5 bytes
    public int ReadVarInt()
    {
        int result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = _reader.ReadByte();
            result |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    //PeekVarInt pre-reads the packet network ID from the start of the stream without affecting the current read position, used to locate errors in logs
    //On a decode failure the read position has been consumed to an arbitrary point by the codec, so reading from 0 is the only way to get the real ID
    //Returns -1 when the stream is exhausted or the VarInt is incomplete; the diagnostic path must never throw
    public int PeekVarInt()
    {
        var position = _stream.Position;
        try
        {
            _stream.Position = 0;
            return ReadVarInt();
        }
        catch (EndOfStreamException)
        {
            return -1;
        }
        finally
        {
            _stream.Position = position;
        }
    }

    //ReadLong reads an 8-byte big-endian long
    public long ReadLong() => BinaryPrimitives.ReadInt64BigEndian(_reader.ReadBytes(8));

    //ReadVarLong reads a variable-length long, at most 10 bytes
    public long ReadVarLong()
    {
        long result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = _reader.ReadByte();
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    //ReadFloat reads a 4-byte big-endian float
    public float ReadFloat() => BinaryPrimitives.ReadSingleBigEndian(_reader.ReadBytes(4));

    //ReadDouble reads an 8-byte big-endian double
    public double ReadDouble() => BinaryPrimitives.ReadDoubleBigEndian(_reader.ReadBytes(8));

    //ReadString reads a UTF-8 string with a VarInt length prefix
    public string ReadString(int maxLength = 32767)
    {
        var length = ReadVarInt();
        if (length > maxLength * 4) throw new InvalidOperationException($"string byte length out of range {length}");
        var bytes = _reader.ReadBytes(length);
        var str = Encoding.UTF8.GetString(bytes);
        if (str.Length > maxLength) throw new InvalidOperationException($"string length out of range {str.Length}");
        return str;
    }

    //ReadByteArray reads a byte array with a VarInt length prefix
    public byte[] ReadByteArray(int maxLength = 32767)
    {
        var length = ReadVarInt();
        if (length > maxLength) throw new InvalidOperationException($"byte array length out of range {length}");
        return _reader.ReadBytes(length);
    }

    //ReadBytes reads a fixed-length byte array
    public byte[] ReadBytes(int length) => _reader.ReadBytes(length);

    //ReadUuid reads a 16-byte big-endian Guid
    public Guid ReadUuid()
    {
        var bytes = _reader.ReadBytes(16);
        //Java UUID is big-endian while .NET Guid uses a mixed-endian internal byte order, so it is built manually
        return new Guid(
            (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]),
            (ushort)(bytes[4] << 8 | bytes[5]),
            (ushort)(bytes[6] << 8 | bytes[7]),
            bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
    }

    //WriteUuid writes a 16-byte big-endian Guid
    public FriendlyByteBuf WriteUuid(Guid value)
    {
        var bytes = value.ToByteArray();
        //.NET Guid.ToByteArray is mixed-endian and must be converted to big-endian
        //Reverses the first 3 segments of 4-2-2 bytes
        Span<byte> buf = stackalloc byte[16];
        buf[0] = bytes[3]; buf[1] = bytes[2]; buf[2] = bytes[1]; buf[3] = bytes[0];
        buf[4] = bytes[5]; buf[5] = bytes[4];
        buf[6] = bytes[7]; buf[7] = bytes[6];
        for (int i = 8; i < 16; i++) buf[i] = bytes[i];
        _writer.Write(buf);
        return this;
    }

    //ReadIdentifier reads an Identifier in namespace:path format
    public NetCraft.Registry.Identifier ReadIdentifier()
    {
        var str = ReadString(32767);
        return NetCraft.Registry.Identifier.Parse(str);
    }

    //WriteIdentifier writes an Identifier as a namespace:path string
    public FriendlyByteBuf WriteIdentifier(NetCraft.Registry.Identifier identifier)
        => WriteString(identifier.ToString());

    //ReadNbt reads an unnamed NBT tag, type byte + payload, maps to vanilla readNbt
    //NBT is self-describing, so the read position stops at the end of the tag and subsequent fields can continue; reading 0 returns EndTag meaning no data
    public NetCraft.Nbt.Tag ReadNbt(NetCraft.Nbt.NbtAccounter? accounter = null)
        => NetCraft.Nbt.NbtIo.ReadAnyTag(new NetCraft.Nbt.BinaryNbtReader(_reader), accounter ?? new NetCraft.Nbt.NbtAccounter());

    //WriteNbt writes an unnamed NBT tag, maps to vanilla writeNbt; null writes a single 0 byte
    public FriendlyByteBuf WriteNbt(NetCraft.Nbt.Tag? tag)
    {
        NetCraft.Nbt.NbtIo.WriteAnyTag(tag ?? NetCraft.Nbt.EndTag.Instance, new NetCraft.Nbt.BinaryNbtWriter(_writer));
        return this;
    }

    //ReadNullable reads an optional value; reader handles the non-null case
    public T? ReadNullable<T>(Func<FriendlyByteBuf, T> reader) where T : class
        => ReadBoolean() ? reader(this) : null;

    //WriteNullable writes an optional value; writer handles the non-null case
    public FriendlyByteBuf WriteNullable<T>(T? value, Action<FriendlyByteBuf, T> writer) where T : class
    {
        if (value == null)
        {
            WriteBoolean(false);
            return this;
        }
        WriteBoolean(true);
        writer(this, value);
        return this;
    }

    //SkipBytes skips the given number of bytes
    public FriendlyByteBuf SkipBytes(int length)
    {
        _reader.ReadBytes(length);
        return this;
    }

    //WriteBoolean writes a 1-byte boolean
    public FriendlyByteBuf WriteBoolean(bool value) { _writer.Write(value); return this; }

    //WriteByte writes 1 byte
    public FriendlyByteBuf WriteByte(byte value) { _writer.Write(value); return this; }

    //WriteShort writes a 2-byte big-endian short
    public FriendlyByteBuf WriteShort(short value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteInt writes a 4-byte big-endian int
    public FriendlyByteBuf WriteInt(int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteVarInt writes a variable-length int
    //Optimization 2.7: batch writes with Span to avoid multiple _writer.Write calls
    public FriendlyByteBuf WriteVarInt(int value)
    {
        Span<byte> buf = stackalloc byte[5];
        int idx = 0;
        var v = (uint)value;
        while ((v & ~0x7Fu) != 0)
        {
            buf[idx++] = (byte)((v & 0x7F) | 0x80);
            v >>>= 7;
        }
        buf[idx++] = (byte)v;
        _writer.Write(buf[..idx]);
        return this;
    }

    //WriteLong writes an 8-byte big-endian long
    public FriendlyByteBuf WriteLong(long value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteLongsBigEndian writes a whole long array big-endian, for the storage blocks that make up most of a chunk packet
    //Per-element WriteLong meant one virtual BinaryWriter call and one MemoryStream bounds check per long, thousands of times per packet,
    //and the profiler put that at the single largest cost in chunk serialization. Batching cuts the stream writes by a factor of 512
    //and the stack buffer keeps it allocation free
    public void WriteLongsBigEndian(long[] values)
    {
        const int BatchLongs = 512;
        Span<byte> batch = stackalloc byte[BatchLongs * 8];
        var offset = 0;
        while (offset < values.Length)
        {
            var count = Math.Min(BatchLongs, values.Length - offset);
            for (var i = 0; i < count; i++)
                BinaryPrimitives.WriteInt64BigEndian(batch.Slice(i * 8, 8), values[offset + i]);
            _writer.Write(batch[..(count * 8)]);
            offset += count;
        }
    }

    //WriteVarLong writes a variable-length long
    //Optimization 2.7: batch writes with Span to avoid multiple _writer.Write calls
    public FriendlyByteBuf WriteVarLong(long value)
    {
        Span<byte> buf = stackalloc byte[10];
        int idx = 0;
        var v = (ulong)value;
        while ((v & ~0x7FUL) != 0)
        {
            buf[idx++] = (byte)((v & 0x7F) | 0x80);
            v >>>= 7;
        }
        buf[idx++] = (byte)v;
        _writer.Write(buf[..idx]);
        return this;
    }

    //WriteFloat writes a 4-byte big-endian float
    public FriendlyByteBuf WriteFloat(float value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteDouble writes an 8-byte big-endian double
    public FriendlyByteBuf WriteDouble(double value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buf, value);
        _writer.Write(buf);
        return this;
    }

    //WriteString writes a UTF-8 string with a VarInt length prefix
    public FriendlyByteBuf WriteString(string value, int maxLength = 32767)
    {
        if (value.Length > maxLength) throw new InvalidOperationException($"string length out of range {value.Length}");
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(bytes.Length);
        _writer.Write(bytes);
        return this;
    }

    //WriteByteArray writes a byte array with a VarInt length prefix
    public FriendlyByteBuf WriteByteArray(byte[] value)
    {
        WriteVarInt(value.Length);
        _writer.Write(value);
        return this;
    }

    //WriteByteArray writes a byte array with a VarInt length prefix and validates the maximum length
    public FriendlyByteBuf WriteByteArray(byte[] value, int maxLength)
    {
        if (value.Length > maxLength)
            throw new ArgumentException($"byte array length {value.Length} exceeds maximum {maxLength}", nameof(value));
        return WriteByteArray(value);
    }

    //WriteBytes writes a fixed-length byte array
    public FriendlyByteBuf WriteBytes(byte[] value) { _writer.Write(value); return this; }

    //WriteBytes writes a slice of a byte array, for a payload whose array is larger than what was written
    public FriendlyByteBuf WriteBytes(byte[] value, int offset, int count) { _writer.Write(value, offset, count); return this; }

    //ToArray returns all bytes of the underlying stream
    public byte[] ToArray() => _stream.ToArray();

    //AsArray returns the bytes in the underlying stream's usable range
    public byte[] AsArray() => _stream.GetBuffer()[..(int)_stream.Length];

    public void Dispose()
    {
        if (_ownsStream)
        {
            _reader.Dispose();
            _writer.Dispose();
            _stream.Dispose();
        }
    }
}
