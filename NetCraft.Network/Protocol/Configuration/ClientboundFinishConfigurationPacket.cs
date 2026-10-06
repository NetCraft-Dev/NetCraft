namespace NetCraft.Network.Protocol.Configuration;

//ClientboundFinishConfigurationPacket the server notifies the client that the configuration phase is complete
//Maps to vanilla net.minecraft.network.protocol.configuration.ClientboundFinishConfigurationPacket
//No payload, using the INSTANCE singleton
//IsTerminal true means switching to the Play protocol after completion
public sealed record ClientboundFinishConfigurationPacket : Packet<ClientConfigurationPacketListener>
{
    //Instance singleton instance
    public static readonly ClientboundFinishConfigurationPacket Instance = new();

    //StreamCodec constant-value codec, maps to vanilla STREAM_CODEC = StreamCodec.unit(INSTANCE)
    public static StreamCodec<FriendlyByteBuf, ClientboundFinishConfigurationPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ClientboundFinishConfigurationPacket>(Instance);

    private ClientboundFinishConfigurationPacket() { }

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundFinishConfiguration;

    //IsTerminal switches to the Play protocol after completion
    public bool IsTerminal => true;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleConfigurationFinished(this);
}
