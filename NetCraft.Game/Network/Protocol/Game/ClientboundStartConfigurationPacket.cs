namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundStartConfigurationPacket start configuration packet, maps to vanilla ClientboundStartConfigurationPacket
//Fields:
public sealed record ClientboundStartConfigurationPacket() : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundStartConfigurationPacket> StreamCodec { get; } = new StartConfigurationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundStartConfiguration;

    public void Handle(ClientGamePacketListener handler) => handler.HandleConfigurationStart(this);

    private sealed class StartConfigurationCodec : StreamCodec<FriendlyByteBuf, ClientboundStartConfigurationPacket>
    {
        public ClientboundStartConfigurationPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ClientboundStartConfigurationPacket value)
            { }
    }
}
