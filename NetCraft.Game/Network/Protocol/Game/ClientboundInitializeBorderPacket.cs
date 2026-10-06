namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundInitializeBorderPacket initialize border packet, maps to vanilla ClientboundInitializeBorderPacket
//Fields: NewCenterX(double), NewCenterZ(double), OldSize(double), NewSize(double), LerpTime(long), NewAbsoluteMaxSize(int)
public sealed record ClientboundInitializeBorderPacket(double NewCenterX, double NewCenterZ, double OldSize, double NewSize, long LerpTime, int NewAbsoluteMaxSize, int WarningBlocks, int WarningTime) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundInitializeBorderPacket> StreamCodec { get; } = new InitializeBorderCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundInitializeBorder;

    public void Handle(ClientGamePacketListener handler) => handler.HandleInitializeBorder(this);

    private sealed class InitializeBorderCodec : StreamCodec<FriendlyByteBuf, ClientboundInitializeBorderPacket>
    {
        public ClientboundInitializeBorderPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble(), buf.ReadDouble(),
                buf.ReadVarLong(), buf.ReadVarInt(), buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundInitializeBorderPacket value)
        {
            buf.WriteDouble(value.NewCenterX);
            buf.WriteDouble(value.NewCenterZ);
            buf.WriteDouble(value.OldSize);
            buf.WriteDouble(value.NewSize);
            buf.WriteVarLong(value.LerpTime);
            buf.WriteVarInt(value.NewAbsoluteMaxSize);
            buf.WriteVarInt(value.WarningBlocks);
            buf.WriteVarInt(value.WarningTime);
        }
    }
}
