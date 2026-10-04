using NetCraft.Primitives;

namespace NetCraft.Network;

//LpVec3 低精度向量对应原版 net.minecraft.network.LpVec3
//三轴各 15 位量化加 2 位指数打包进 6 字节 实体交互的命中位置用它省带宽
public static class LpVec3
{
    //AbsMaxValue 单轴绝对值上限对应原版 ABS_MAX_VALUE
    public const double AbsMaxValue = 1.7179869183E10;
    //AbsMinValue 棋盘长度低于它按零向量写对应原版 ABS_MIN_VALUE
    public const double AbsMinValue = 3.051944088384301E-5;

    private const int DataBitsMask = 32767;
    private const double MaxQuantizedValue = 32766.0;
    private const int ScaleBitsMask = 3;
    private const int ContinuationFlag = 4;

    //Read 读低精度向量 首字节为 0 即零向量
    public static Vec3 Read(FriendlyByteBuf buf)
    {
        var lowest = buf.ReadByte();
        if (lowest == 0) return Vec3.Zero;
        var middle = buf.ReadByte();
        var highest = (uint)buf.ReadInt();
        var buffer = ((long)highest << 16) | ((long)middle << 8) | lowest;
        long scale = lowest & ScaleBitsMask;
        //有 continuation 位说明指数放不下 2 位 后面还跟一个 VarInt 表示 scale >> 2
        if ((lowest & ContinuationFlag) != 0)
            scale |= (long)(uint)buf.ReadVarInt() << 2;
        return new Vec3(
            Unpack(buffer >> 3) * scale,
            Unpack(buffer >> 18) * scale,
            Unpack(buffer >> 33) * scale);
    }

    //Write 写低精度向量 与 Read 对称
    public static void Write(FriendlyByteBuf buf, Vec3 value)
    {
        var x = Sanitize(value.X);
        var y = Sanitize(value.Y);
        var z = Sanitize(value.Z);
        var chessboardLength = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (chessboardLength < AbsMinValue)
        {
            buf.WriteByte(0);
            return;
        }
        var scale = (long)Math.Ceiling(chessboardLength);
        //指数超出 2 位时低两位存 markers 高位另发 VarInt
        var isPartial = (scale & ScaleBitsMask) != scale;
        var markers = isPartial ? (scale & ScaleBitsMask) | ContinuationFlag : scale;
        var buffer = markers
            | (Pack(x / scale) << 3)
            | (Pack(y / scale) << 18)
            | (Pack(z / scale) << 33);
        buf.WriteByte((byte)buffer);
        buf.WriteByte((byte)(buffer >> 8));
        buf.WriteInt((int)(buffer >> 16));
        if (isPartial) buf.WriteVarInt((int)(scale >> 2));
    }

    //Sanitize NaN 归零并夹到上下限
    private static double Sanitize(double value)
        => double.IsNaN(value) ? 0.0 : Math.Clamp(value, -AbsMaxValue, AbsMaxValue);

    //Pack 归一化值量化到 15 位 原版 Math.round 是 floor(x+0.5) 不能用 C# 默认的银行家舍入
    private static long Pack(double value)
        => (long)Math.Floor((value * 0.5 + 0.5) * MaxQuantizedValue + 0.5);

    //Unpack 15 位量化值还原为 -1..1
    private static double Unpack(long value)
        => (Math.Min((double)(value & DataBitsMask), MaxQuantizedValue) * 2.0 / MaxQuantizedValue) - 1.0;
}
