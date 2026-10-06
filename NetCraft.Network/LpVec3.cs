using NetCraft.Primitives;

namespace NetCraft.Network;

//LpVec3 low-precision vector, maps to vanilla net.minecraft.network.LpVec3
//Three axes each quantized to 15 bits plus a 2-bit exponent packed into 6 bytes; used for entity interaction hit positions to save bandwidth
public static class LpVec3
{
    //AbsMaxValue is the per-axis absolute value cap, maps to vanilla ABS_MAX_VALUE
    public const double AbsMaxValue = 1.7179869183E10;
    //AbsMinValue: a chebyshev length below it is written as a zero vector, maps to vanilla ABS_MIN_VALUE
    public const double AbsMinValue = 3.051944088384301E-5;

    private const int DataBitsMask = 32767;
    private const double MaxQuantizedValue = 32766.0;
    private const int ScaleBitsMask = 3;
    private const int ContinuationFlag = 4;

    //Read reads a low-precision vector; a first byte of 0 means a zero vector
    public static Vec3 Read(FriendlyByteBuf buf)
    {
        var lowest = buf.ReadByte();
        if (lowest == 0) return Vec3.Zero;
        var middle = buf.ReadByte();
        var highest = (uint)buf.ReadInt();
        var buffer = ((long)highest << 16) | ((long)middle << 8) | lowest;
        long scale = lowest & ScaleBitsMask;
        //A continuation bit means the exponent does not fit in 2 bits, followed by a VarInt holding scale >> 2
        if ((lowest & ContinuationFlag) != 0)
            scale |= (long)(uint)buf.ReadVarInt() << 2;
        return new Vec3(
            Unpack(buffer >> 3) * scale,
            Unpack(buffer >> 18) * scale,
            Unpack(buffer >> 33) * scale);
    }

    //Write writes a low-precision vector, symmetric with Read
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
        //When the exponent exceeds 2 bits, the low 2 bits store markers and the high bits are sent as a separate VarInt
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

    //Sanitize zeroes out NaN and clamps to the min and max bounds
    private static double Sanitize(double value)
        => double.IsNaN(value) ? 0.0 : Math.Clamp(value, -AbsMaxValue, AbsMaxValue);

    //Pack quantizes the normalized value to 15 bits; vanilla Math.round is floor(x+0.5), so C#'s default banker's rounding must not be used
    private static long Pack(double value)
        => (long)Math.Floor((value * 0.5 + 0.5) * MaxQuantizedValue + 0.5);

    //Unpack restores a 15-bit quantized value to -1..1
    private static double Unpack(long value)
        => (Math.Min((double)(value & DataBitsMask), MaxQuantizedValue) * 2.0 / MaxQuantizedValue) - 1.0;
}
