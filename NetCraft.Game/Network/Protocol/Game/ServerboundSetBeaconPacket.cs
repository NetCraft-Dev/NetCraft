namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetBeaconPacket 数据包对应原版 ServerboundSetBeaconPacket
//字段 Primary(int 主效果注册表 id 可为空) Secondary(int 副效果注册表 id 可为空)
public sealed record ServerboundSetBeaconPacket(int? Primary, int? Secondary) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetBeaconPacket> StreamCodec { get; } = new SetBeaconCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetBeacon;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetBeaconPacket(this);

    private sealed class SetBeaconCodec : StreamCodec<FriendlyByteBuf, ServerboundSetBeaconPacket>
    {
        //信标界面确认时发送 原版两个效果都是 Optional 各自先写存在标志再写 varint
        public ServerboundSetBeaconPacket Decode(FriendlyByteBuf buf)
        {
            int? primary = buf.ReadBoolean() ? buf.ReadVarInt() : null;
            int? secondary = buf.ReadBoolean() ? buf.ReadVarInt() : null;
            return new(primary, secondary);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundSetBeaconPacket value)
        {
            buf.WriteBoolean(value.Primary.HasValue);
            if (value.Primary.HasValue) buf.WriteVarInt(value.Primary.Value);
            buf.WriteBoolean(value.Secondary.HasValue);
            if (value.Secondary.HasValue) buf.WriteVarInt(value.Secondary.Value);
        }
    }
}
