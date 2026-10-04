using System.IO.Compression;

namespace NetCraft.Network;

//CompressionHelper 包压缩辅助对应原版 CompressionEncoder/CompressionDecoder
//原版用 zlib deflate 这里用 .NET DeflateStream
//包格式未压缩 [数据长度=0][Packet ID][数据]
//包格式压缩   [数据长度>0][deflate([Packet ID][数据])]
public static class CompressionHelper
{
    //CompressIfNeeded 数据长度超阈值才压缩否则标记 0 不压缩
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

    //Decompress 解压包数据按数据长度前缀判断是否压缩
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
            if (b < 0) throw new EndOfStreamException("VarInt 读取提前结束");
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
