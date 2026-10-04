namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetBorderWarningDelayPacket 边界警告延迟包对应原版 ClientboundSetBorderWarningDelayPacket
//字段 WarningDelay(int)
public sealed record ClientboundSetBorderWarningDelayPacket(int WarningDelay) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetBorderWarningDelayPacket> StreamCodec { get; } = new SetBorderWarningDelayCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetBorderWarningDelay;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetBorderWarningDelay(this);

    private sealed class SetBorderWarningDelayCodec : StreamCodec<FriendlyByteBuf, ClientboundSetBorderWarningDelayPacket>
    {
        public ClientboundSetBorderWarningDelayPacket Decode(FriendlyByteBuf buf) => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetBorderWarningDelayPacket value)
            => buf.WriteVarInt(value.WarningDelay);
    }
}
