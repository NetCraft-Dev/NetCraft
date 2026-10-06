namespace NetCraft.Network.Protocol.Configuration;

//ServerboundFinishConfigurationPacket the client notifies the server that the configuration phase is complete
//Maps to vanilla net.minecraft.network.protocol.configuration.ServerboundFinishConfigurationPacket
//No payload, using the INSTANCE singleton
public sealed record ServerboundFinishConfigurationPacket : Packet<ServerConfigurationPacketListener>
{
    public static readonly ServerboundFinishConfigurationPacket Instance = new();

    public static StreamCodec<FriendlyByteBuf, ServerboundFinishConfigurationPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundFinishConfigurationPacket>(Instance);

    private ServerboundFinishConfigurationPacket() { }

    public PacketType<ServerConfigurationPacketListener> Type => ConfigurationPacketTypes.ServerboundFinishConfiguration;

    public void Handle(ServerConfigurationPacketListener handler) => handler.HandleConfigurationFinished(this);
}
