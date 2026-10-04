using NetCraft.Primitives;
using NetCraft.Network;
using NetCraft.Util;

namespace NetCraft.Game.Network;

//FriendlyByteBufExtensions 业务包需要的扩展方法
//FriendlyByteBuf 内核只保留基础类型读写业务类型 BlockPos/SectionPos/Enum 等在此扩展
public static class FriendlyByteBufExtensions
{
    //ReadUnsignedByte 读 1 字节为 int 对齐原版 readUnsignedByte
    public static int ReadUnsignedByte(this FriendlyByteBuf buf)
        => buf.ReadByte();

    //ReadBlockPos 读 packed long 还原 BlockPos
    public static BlockPos ReadBlockPos(this FriendlyByteBuf buf)
        => BlockPos.FromLong(buf.ReadLong());

    //WriteBlockPos 写 BlockPos 为 packed long
    public static FriendlyByteBuf WriteBlockPos(this FriendlyByteBuf buf, BlockPos pos)
        => buf.WriteLong(pos.AsLong());

    //ReadSectionPos 读 packed long 还原 SectionPos
    public static SectionPos ReadSectionPos(this FriendlyByteBuf buf)
        => SectionPos.Of(buf.ReadLong());

    //WriteSectionPos 写 SectionPos 为 packed long
    public static FriendlyByteBuf WriteSectionPos(this FriendlyByteBuf buf, SectionPos pos)
        => buf.WriteLong(pos.AsLong());

    //ReadBlockHitResult 读方块命中结果 字段顺序严格对齐原版 readBlockHitResult
    //坐标 -> 面(VarInt) -> 命中点相对偏移(float*3) -> 是否内部 -> 是否撞世界边界
    //末尾的 worldBorder 位不能省: 省掉它紧接着读的 sequence 会读到这一位
    //客户端记录的预测序号是 1 而回执变成 0 服务端就永远清不掉客户端的预测 方块的后续更新全被缓存
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

    //WriteBlockHitResult 写方块命中结果 与原版 writeBlockHitResult 逐字段一致
    //命中点写的是相对方块原点的偏移 不是绝对坐标
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

    //ReadEnum 读 VarInt 还原枚举值按声明顺序
    public static T ReadEnum<T>(this FriendlyByteBuf buf) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int ordinal = buf.ReadVarInt();
        return values[ordinal];
    }

    //WriteEnum 写枚举值的声明顺序为 VarInt
    public static FriendlyByteBuf WriteEnum<T>(this FriendlyByteBuf buf, T value) where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int ordinal = Array.IndexOf(values, value);
        return buf.WriteVarInt(ordinal);
    }

    //ReadIntIdList 读 VarInt 长度前缀的 int 数组
    public static int[] ReadIntIdList(this FriendlyByteBuf buf)
    {
        int length = buf.ReadVarInt();
        int[] ids = new int[length];
        for (int i = 0; i < length; i++)
            ids[i] = buf.ReadVarInt();
        return ids;
    }

    //WriteIntIdList 写 int 数组为 VarInt 长度前缀 + VarInt 数组
    public static FriendlyByteBuf WriteIntIdList(this FriendlyByteBuf buf, int[] ids)
    {
        buf.WriteVarInt(ids.Length);
        foreach (int id in ids)
            buf.WriteVarInt(id);
        return buf;
    }

    //LpAbsMinValue/LpAbsMaxValue 低精度向量的有效分量下限与上限 对应原版 LpVec3
    private const double LpAbsMinValue = 3.051944088384301E-5d;
    private const double LpAbsMaxValue = 1.7179869183E10d;
    private const int LpMaxQuantizedValue = 32766;

    //ReadLpVec3 读低精度量化向量对应原版 LpVec3.read
    //1 字节 0 表示零向量 否则 15 位量化分量乘缩放系数 缩放高位按需续读 VarInt
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

    //WriteLpVec3 写低精度量化向量对应原版 LpVec3.write
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

    //LpPack 归一化分量量化到 15 位
    private static long LpPack(double value)
        => (long)Math.Round((value * 0.5d + 0.5d) * LpMaxQuantizedValue);

    //LpUnpack 15 位量化值还原为 -1~1 的分量
    private static double LpUnpack(long value)
        => (Math.Min(value & 32767, LpMaxQuantizedValue) * 2.0d / LpMaxQuantizedValue) - 1.0d;

    //LpSanitize NaN 归零并把分量夹到有效范围
    private static double LpSanitize(double value)
        => double.IsNaN(value) ? 0d : Math.Clamp(value, -LpAbsMaxValue, LpAbsMaxValue);
}
