using System.IO.Compression;

namespace NetCraft.Network;

//CompressionHelper packet compression helper, maps to vanilla CompressionEncoder/CompressionDecoder
//Vanilla uses zlib deflate; here we use .NET DeflateStream
//Uncompressed packet format: [data length=0][Packet ID][data]
//Compressed packet format:   [data length>0][deflate([Packet ID][data])]
public static class CompressionHelper
{
    //CompressIfNeeded compresses only when the data length exceeds the threshold, otherwise marks 0 to skip compression
    public static byte[] CompressIfNeeded(byte[] data, int threshold)
    {
        using var outBuf = new MemoryStream();
        if (data.Length < threshold)
        {
            WriteVarInt(outBuf, 0);
            outBuf.Write(data, 0, data.Length);
        }
        else
        {
            using var compressed = new MemoryStream();
            using (var deflate = new DeflateStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }
            byte[] compressedData = compressed.ToArray();
            WriteVarInt(outBuf, compressedData.Length);
            outBuf.Write(compressedData, 0, compressedData.Length);
        }
        return outBuf.ToArray();
    }

    //Decompress decompresses packet data, judging compression by the data length prefix
    public static byte[] Decompress(byte[] payload)
    {
        using var inBuf = new MemoryStream(payload);
        int dataLen = ReadVarInt(inBuf);
        if (dataLen == 0)
        {
            return ReadRest(inBuf);
        }
        using var deflate = new DeflateStream(inBuf, CompressionMode.Decompress, leaveOpen: true);
        using var outBuf = new MemoryStream();
        deflate.CopyTo(outBuf);
        return outBuf.ToArray();
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var v = (uint)value;
        while ((v & ~0x7Fu) != 0)
        {
            stream.WriteByte((byte)((v & 0x7F) | 0x80));
            v >>>= 7;
        }
        stream.WriteByte((byte)v);
    }

    private static int ReadVarInt(Stream stream)
    {
        int result = 0;
        int shift = 0;
        int b;
        do
        {
            b = stream.ReadByte();
            if (b < 0) throw new EndOfStreamException("VarInt ended early");
            result |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }

    private static byte[] ReadRest(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
