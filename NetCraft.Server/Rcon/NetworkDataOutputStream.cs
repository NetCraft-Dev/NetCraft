using System.Text;

namespace NetCraft.Game.Server.Rcon;

//NetworkDataOutputStream RCON 与查询协议的组包缓冲对应原版 net.minecraft.server.rcon.NetworkDataOutputStream
//原版在 Java 大端流上再反转字节 等价于线上小端 这里直接按小端写
//字符串是 UTF-8 字节后跟一个 0 终止符
public class NetworkDataOutputStream
{
    private readonly MemoryStream _output;

    public NetworkDataOutputStream(int size) => _output = new MemoryStream(size);

    //WriteBytes 追加原始字节
    public void WriteBytes(byte[] data) => _output.Write(data, 0, data.Length);

    //WriteString 追加 UTF-8 字符串与 0 终止符
    public void WriteString(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        _output.Write(bytes, 0, bytes.Length);
        _output.WriteByte(0);
    }

    //Write 写单个字节
    public void Write(int data) => _output.WriteByte((byte)data);

    //WriteShort 写小端 short
    public void WriteShort(short data)
    {
        _output.WriteByte((byte)data);
        _output.WriteByte((byte)(data >> 8));
    }

    //WriteInt 写小端 int
    public void WriteInt(int data)
    {
        _output.WriteByte((byte)data);
        _output.WriteByte((byte)(data >> 8));
        _output.WriteByte((byte)(data >> 16));
        _output.WriteByte((byte)(data >> 24));
    }

    //WriteFloat 写小端 float 位型按 int 处理
    public void WriteFloat(float data) => WriteInt(BitConverter.SingleToInt32Bits(data));

    //ToByteArray 取当前缓冲
    public byte[] ToByteArray() => _output.ToArray();

    //Reset 清空缓冲
    public void Reset() => _output.SetLength(0);
}
