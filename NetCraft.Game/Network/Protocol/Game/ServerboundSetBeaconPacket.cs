namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetBeaconPacket set beacon packet, maps to vanilla ServerboundSetBeaconPacket
//Fields: Primary(int primary effect registry id, may be empty), Secondary(int secondary effect registry id, may be empty)
public sealed record ServerboundSetBeaconPacket(int? Primary, int? Secondary) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSetBeaconPacket> StreamCodec { get; } = new SetBeaconCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetBeacon;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetBeaconPacket(this);

    private sealed class SetBeaconCodec : StreamCodec<FriendlyByteBuf, ServerboundSetBeaconPacket>
    {
        //Sent when confirming in the beacon screen; both effects are Optional in vanilla, each writing a presence flag before the varint
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
