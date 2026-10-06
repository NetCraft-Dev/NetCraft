using System.Text;

namespace NetCraft.Game.Server.Rcon;

//NetworkDataOutputStream, packet building buffer for RCON and the query protocol, maps to vanilla net.minecraft.server.rcon.NetworkDataOutputStream
//Vanilla writes big-endian on a Java stream and then reverses the bytes, equivalent to little-endian on the wire, so this writes little-endian directly
//A string is UTF-8 bytes followed by a 0 terminator
public class NetworkDataOutputStream
{
    private readonly MemoryStream _output;

    public NetworkDataOutputStream(int size) => _output = new MemoryStream(size);

    //WriteBytes appends raw bytes
    public void WriteBytes(byte[] data) => _output.Write(data, 0, data.Length);

    //WriteString appends a UTF-8 string with a 0 terminator
    public void WriteString(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        _output.Write(bytes, 0, bytes.Length);
        _output.WriteByte(0);
    }

    //Write writes a single byte
    public void Write(int data) => _output.WriteByte((byte)data);

    //WriteShort writes a little-endian short
    public void WriteShort(short data)
    {
        _output.WriteByte((byte)data);
        _output.WriteByte((byte)(data >> 8));
    }

    //WriteInt writes a little-endian int
    public void WriteInt(int data)
    {
        _output.WriteByte((byte)data);
        _output.WriteByte((byte)(data >> 8));
        _output.WriteByte((byte)(data >> 16));
        _output.WriteByte((byte)(data >> 24));
    }

    //WriteFloat writes a little-endian float, its bit pattern handled as an int
    public void WriteFloat(float data) => WriteInt(BitConverter.SingleToInt32Bits(data));

    //ToByteArray returns the current buffer
    public byte[] ToByteArray() => _output.ToArray();

    //Reset clears the buffer
    public void Reset() => _output.SetLength(0);
}
