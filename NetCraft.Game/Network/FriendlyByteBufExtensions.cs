using NetCraft.Primitives;
using NetCraft.Network;
using NetCraft.Util;

namespace NetCraft.Game.Network;

//FriendlyByteBufExtensions extension methods needed by business packets
//The FriendlyByteBuf kernel only keeps primitive type reads/writes; business types like BlockPos/SectionPos/Enum are extended here
public static class FriendlyByteBufExtensions
{
    //ReadUnsignedByte reads 1 byte as an int, aligns with vanilla readUnsignedByte
    public static int ReadUnsignedByte(this FriendlyByteBuf buf)
        => buf.ReadByte();

    //ReadBlockPos reads a packed long and restores a BlockPos
    public static BlockPos ReadBlockPos(this FriendlyByteBuf buf)
        => BlockPos.FromLong(buf.ReadLong());

    //WriteBlockPos writes a BlockPos as a packed long
    public static FriendlyByteBuf WriteBlockPos(this FriendlyByteBuf buf, BlockPos pos)
        => buf.WriteLong(pos.AsLong());

    //ReadSectionPos reads a packed long and restores a SectionPos
    public static SectionPos ReadSectionPos(this FriendlyByteBuf buf)
        => SectionPos.Of(buf.ReadLong());

    //WriteSectionPos writes a SectionPos as a packed long
    public static FriendlyByteBuf WriteSectionPos(this FriendlyByteBuf buf, SectionPos pos)
        => buf.WriteLong(pos.AsLong());

    //ReadBlockHitResult reads a block hit result; field order strictly aligns with vanilla readBlockHitResult
    //Position -> face (VarInt) -> hit offset relative to the block (float*3) -> inside -> world border hit
    //The trailing worldBorder bit cannot be dropped: without it the following sequence read would consume this bit
    //The client records sequence 1 but the ack turns it into 0, so the server can never clear the client prediction and all later block updates stay cached
    public static BlockHitResult ReadBlockHitResult(this FriendlyByteBuf buf)
    {
        var pos = buf.ReadBlockPos();
        var direction = Direction.ById(buf.ReadVarInt());
        var clickX = buf.ReadFloat();
        var clickY = buf.ReadFloat();
        var clickZ = buf.ReadFloat();
        var inside = buf.ReadBoolean();
        var worldBorder = buf.ReadBoolean();
        return new BlockHitResult(pos, direction,
            new Vec3(pos.X + clickX, pos.Y + clickY, pos.Z + clickZ), inside, worldBorder);
    }

    //WriteBlockHitResult writes a block hit result, field-by-field identical to vanilla writeBlockHitResult
    //The hit point is written as an offset relative to the block origin, not absolute coordinates
    public static FriendlyByteBuf WriteBlockHitResult(this FriendlyByteBuf buf, BlockHitResult hit)
    {
        buf.WriteBlockPos(hit.BlockPos);
        buf.WriteVarInt(hit.Direction.Id3D);
        buf.WriteFloat((float)(hit.Location.X - hit.BlockPos.X));
        buf.WriteFloat((float)(hit.Location.Y - hit.BlockPos.Y));
        buf.WriteFloat((float)(hit.Location.Z - hit.BlockPos.Z));
        buf.WriteBoolean(hit.Inside);
        return buf.WriteBoolean(hit.WorldBorderHit);
    }

    //ReadEnum reads a VarInt and restores the enum value by declaration order
    public static T ReadEnum<T>(this FriendlyByteBuf buf) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int ordinal = buf.ReadVarInt();
        return values[ordinal];
    }

    //WriteEnum writes the enum value's declaration order as a VarInt
    public static FriendlyByteBuf WriteEnum<T>(this FriendlyByteBuf buf, T value) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int ordinal = Array.IndexOf(values, value);
        return buf.WriteVarInt(ordinal);
    }

    //ReadIntIdList reads an int array with a VarInt length prefix
    public static int[] ReadIntIdList(this FriendlyByteBuf buf)
    {
        int length = buf.ReadVarInt();
        int[] ids = new int[length];
        for (int i = 0; i < length; i++)
            ids[i] = buf.ReadVarInt();
        return ids;
    }

    //WriteIntIdList writes an int array as a VarInt length prefix + VarInt array
    public static FriendlyByteBuf WriteIntIdList(this FriendlyByteBuf buf, int[] ids)
    {
        buf.WriteVarInt(ids.Length);
        foreach (int id in ids)
            buf.WriteVarInt(id);
        return buf;
    }

    //LpAbsMinValue/LpAbsMaxValue lower and upper bounds of a valid component of a low-precision vector, maps to vanilla LpVec3
    private const double LpAbsMinValue = 3.051944088384301E-5d;
    private const double LpAbsMaxValue = 1.7179869183E10d;
    private const int LpMaxQuantizedValue = 32766;

    //ReadLpVec3 reads a low-precision quantized vector, maps to vanilla LpVec3.read
    //A first byte of 0 means a zero vector; otherwise 15-bit quantized components times the scale factor, with the scale high bits read as a continuation VarInt when needed
    public static Vec3 ReadLpVec3(this FriendlyByteBuf buf)
    {
        int lowest = buf.ReadUnsignedByte();
        if (lowest == 0) return Vec3.Zero;
        int middle = buf.ReadUnsignedByte();
        long highest = (uint)buf.ReadInt();
        long buffer = (highest << 16) | ((long)middle << 8) | (uint)lowest;
        long scale = lowest & 3;
        if ((lowest & 4) == 4) scale |= (long)(uint)buf.ReadVarInt() << 2;
        return new Vec3(
            LpUnpack(buffer >> 3) * scale,
            LpUnpack(buffer >> 18) * scale,
            LpUnpack(buffer >> 33) * scale);
    }

    //WriteLpVec3 writes a low-precision quantized vector, maps to vanilla LpVec3.write
    public static FriendlyByteBuf WriteLpVec3(this FriendlyByteBuf buf, Vec3 value)
    {
        double x = LpSanitize(value.X);
        double y = LpSanitize(value.Y);
        double z = LpSanitize(value.Z);
        double chessboardLength = Mth.AbsMax(x, Mth.AbsMax(y, z));
        if (chessboardLength < LpAbsMinValue) return buf.WriteByte(0);
        long scale = Mth.CeilLong(chessboardLength);
        bool isPartial = (scale & 3) != scale;
        long markers = isPartial ? (scale & 3) | 4 : scale;
        long buffer = markers
            | (LpPack(x / scale) << 3)
            | (LpPack(y / scale) << 18)
            | (LpPack(z / scale) << 33);
        buf.WriteByte((byte)buffer);
        buf.WriteByte((byte)(buffer >> 8));
        buf.WriteInt((int)(buffer >> 16));
        if (isPartial) buf.WriteVarInt((int)(scale >> 2));
        return buf;
    }

    //LpPack quantizes a normalized component to 15 bits
    private static long LpPack(double value)
        => (long)Math.Round((value * 0.5d + 0.5d) * LpMaxQuantizedValue);

    //LpUnpack restores a 15-bit quantized value to a component in -1~1
    private static double LpUnpack(long value)
        => (Math.Min(value & 32767, LpMaxQuantizedValue) * 2.0d / LpMaxQuantizedValue) - 1.0d;

    //LpSanitize zeroes out NaN and clamps the component to the valid range
    private static double LpSanitize(double value)
        => double.IsNaN(value) ? 0d : Math.Clamp(value, -LpAbsMaxValue, LpAbsMaxValue);
}
