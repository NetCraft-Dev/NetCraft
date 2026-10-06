using System.Text;

namespace NetCraft.Game.Server.Rcon;

//PktUtils RCON 协议字节工具对应原版 net.minecraft.server.rcon.PktUtils
//intFromByteArray 是小端读 intFromNetworkByteArray 是大端读 两个方向各有用途
public class PktUtils
{
    public const int MaxPacketSize = 1460;
    public const string HexChar = "0123456789abcdef";

    //StringFromByteArray 从 offset 起读到 0 终止符与原版同一套越界收缩逻辑
    public static string StringFromByteArray(byte[] b, int offset, int length)
    {
        var max = length - 1;
        var i = offset > max ? max : offset;
        while (b[i] != 0 && i < max) ++i;
        return Encoding.UTF8.GetString(b, offset, i - offset);
    }

    //IntFromByteArray 小端读 4 字节不足 4 字节返回 0
    public static int IntFromByteArray(byte[] b, int offset) => IntFromByteArray(b, offset, b.Length);

    public static int IntFromByteArray(byte[] b, int offset, int length)
    {
        if (0 > length - offset - 4) return 0;
        return b[offset + 3] << 24 | (b[offset + 2] & 0xFF) << 16 | (b[offset + 1] & 0xFF) << 8 | b[offset] & 0xFF;
    }

    //IntFromNetworkByteArray 大端读 4 字节
    public static int IntFromNetworkByteArray(byte[] b, int offset, int length)
    {
        if (0 > length - offset - 4) return 0;
        return b[offset] << 24 | (b[offset + 1] & 0xFF) << 16 | (b[offset + 2] & 0xFF) << 8 | b[offset + 3] & 0xFF;
    }

    //ToHexString 单字节十六进制
    public static string ToHexString(byte b) => $"{HexChar[(b & 0xF0) >> 4]}{HexChar[b & 0xF]}";
}
