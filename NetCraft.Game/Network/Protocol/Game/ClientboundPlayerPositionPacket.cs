using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//RelativeFlag 玩家位置相对移动标志位号对应原版 Relative
//X/Y/Z 位置分量相对 YRot/XRot 朝向分量相对 Delta 三轴速度相对 RotateDelta 速度随朝向差旋转
public enum RelativeFlag
{
    X = 0,
    Y = 1,
    Z = 2,
    YRot = 3,
    XRot = 4,
    DeltaX = 5,
    DeltaY = 6,
    DeltaZ = 7,
    RotateDelta = 8,
}

//RelativeFlags 相对标志集合运算对应原版 Relative 的静态集与位掩码编解码
public static class RelativeFlags
{
    //Rotation 纯朝向相对集 传送未显式给朝向时保持当前朝向
    public static readonly IReadOnlySet<RelativeFlag> Rotation = new HashSet<RelativeFlag> { RelativeFlag.YRot, RelativeFlag.XRot };

    //Delta 纯速度相对集
    public static readonly IReadOnlySet<RelativeFlag> Delta = new HashSet<RelativeFlag> { RelativeFlag.DeltaX, RelativeFlag.DeltaY, RelativeFlag.DeltaZ, RelativeFlag.RotateDelta };

    //Direction 速度相对集 坐标相对时保留当前速度
    public static IReadOnlySet<RelativeFlag> Direction(bool x, bool y, bool z)
    {
        var set = new HashSet<RelativeFlag>();
        if (x) set.Add(RelativeFlag.DeltaX);
        if (y) set.Add(RelativeFlag.DeltaY);
        if (z) set.Add(RelativeFlag.DeltaZ);
        return set;
    }

    //Position 位置相对集 客户端把包值加到当前坐标
    public static IReadOnlySet<RelativeFlag> Position(bool x, bool y, bool z)
    {
        var set = new HashSet<RelativeFlag>();
        if (x) set.Add(RelativeFlag.X);
        if (y) set.Add(RelativeFlag.Y);
        if (z) set.Add(RelativeFlag.Z);
        return set;
    }

    //RotationOf 朝向相对集
    public static IReadOnlySet<RelativeFlag> RotationOf(bool yRot, bool xRot)
    {
        var set = new HashSet<RelativeFlag>();
        if (yRot) set.Add(RelativeFlag.YRot);
        if (xRot) set.Add(RelativeFlag.XRot);
        return set;
    }

    //Union 合并多个集合
    public static IReadOnlySet<RelativeFlag> Union(params IReadOnlySet<RelativeFlag>[] sets)
    {
        var result = new HashSet<RelativeFlag>();
        foreach (var set in sets) result.UnionWith(set);
        return result;
    }

    //Pack 集合转位掩码
    public static int Pack(IEnumerable<RelativeFlag> set)
    {
        var result = 0;
        foreach (var flag in set) result |= 1 << (int)flag;
        return result;
    }

    //Unpack 掩码还原集合
    public static IReadOnlyList<RelativeFlag> Unpack(int value)
    {
        var result = new List<RelativeFlag>();
        foreach (var flag in Enum.GetValues<RelativeFlag>())
            if ((value & (1 << (int)flag)) != 0) result.Add(flag);
        return result;
    }
}

//ClientboundPlayerPositionPacket 玩家位置同步包对应原版 ClientboundPlayerPositionPacket
//S4 26.2 重构为 PositionMoveRotation 格式
//字段 Id(VarInt) X/Y/Z(Double 位置) DX/DY/DZ(Double deltaMovement) YRot/XRot(Float) Relatives(Int 位掩码)
public sealed record ClientboundPlayerPositionPacket(
    double X,
    double Y,
    double Z,
    float YRot,
    float XRot,
    int Relatives,
    int Id) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerPositionPacket> StreamCodec { get; } = new PlayerPositionCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerPosition;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMovePlayer(this);

    private sealed class PlayerPositionCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerPositionPacket>
    {
        public ClientboundPlayerPositionPacket Decode(FriendlyByteBuf buf)
        {
            //26.2: id varint + position(3 double) + deltaMovement(3 double) + yRot/xRot float + relatives int
            var id = buf.ReadVarInt();
            var x = buf.ReadDouble();
            var y = buf.ReadDouble();
            var z = buf.ReadDouble();
            buf.ReadDouble();
            buf.ReadDouble();
            buf.ReadDouble();
            var yRot = buf.ReadFloat();
            var xRot = buf.ReadFloat();
            var relatives = buf.ReadInt();
            return new ClientboundPlayerPositionPacket(x, y, z, yRot, xRot, relatives, id);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerPositionPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteDouble(value.X);
            buf.WriteDouble(value.Y);
            buf.WriteDouble(value.Z);
            //deltaMovement 默认 0 对齐 PositionMoveRotation.deltaMovement
            buf.WriteDouble(0);
            buf.WriteDouble(0);
            buf.WriteDouble(0);
            buf.WriteFloat(value.YRot);
            buf.WriteFloat(value.XRot);
            //Relative.SET_STREAM_CODEC int 位掩码
            buf.WriteInt(value.Relatives);
        }
    }
}
