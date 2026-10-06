using System.Text;

namespace NetCraft.Game.Server.Rcon;

//PktUtils, RCON protocol byte utilities, maps to vanilla net.minecraft.server.rcon.PktUtils
//intFromByteArray reads little-endian, intFromNetworkByteArray reads big-endian, both directions are used
public class PktUtils
{
    public const int MaxPacketSize = 1460;
    public const string HexChar = "0123456789abcdef";

    //StringFromByteArray reads from offset until the 0 terminator, same out-of-range clamping logic as vanilla
    public static string StringFromByteArray(byte[] b, int offset, int length)
    {
        var max = length - 1;
        var i = offset > max ? max : offset;
        while (b[i] != 0 && i < max) ++i;
        return Encoding.UTF8.GetString(b, offset, i - offset);
    }

    //IntFromByteArray reads 4 bytes little-endian, returns 0 when fewer than 4 bytes are available
    public static int IntFromByteArray(byte[] b, int offset) => IntFromByteArray(b, offset, b.Length);

    public static int IntFromByteArray(byte[] b, int offset, int length)
    {
        if (0 > length - offset - 4) return 0;
        return b[offset + 3] << 24 | (b[offset + 2] & 0xFF) << 16 | (b[offset + 1] & 0xFF) << 8 | b[offset] & 0xFF;
    }

    //IntFromNetworkByteArray reads 4 bytes big-endian
    public static int IntFromNetworkByteArray(byte[] b, int offset, int length)
    {
        if (0 > length - offset - 4) return 0;
        return b[offset] << 24 | (b[offset + 1] & 0xFF) << 16 | (b[offset + 2] & 0xFF) << 8 | b[offset + 3] & 0xFF;
    }

    //ToHexString single-byte hex
    public static string ToHexString(byte b) => $"{HexChar[(b & 0xF0) >> 4]}{HexChar[b & 0xF]}";
}
