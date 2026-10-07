namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundConfigurationAcknowledgedPacket configuration acknowledged packet, maps to vanilla ServerboundConfigurationAcknowledgedPacket
//Fields:
public sealed record ServerboundConfigurationAcknowledgedPacket() : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundConfigurationAcknowledgedPacket> StreamCodec { get; } = new ConfigurationAcknowledgedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundConfigurationAcknowledged;

    public void Handle(ServerGamePacketListener handler) => handler.HandleConfigurationAcknowledged(this);

    private sealed class ConfigurationAcknowledgedCodec : StreamCodec<FriendlyByteBuf, ServerboundConfigurationAcknowledgedPacket>
    {
        public ServerboundConfigurationAcknowledgedPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ServerboundConfigurationAcknowledgedPacket value)
            { }
    }
}
