namespace NetCraft.Network.Protocol.Configuration;

//ClientboundResetChatPacket the server notifies the client to reset chat state
//Maps to vanilla net.minecraft.network.protocol.configuration.ClientboundResetChatPacket
//No payload, using the INSTANCE singleton
public sealed record ClientboundResetChatPacket : Packet<ClientConfigurationPacketListener>
{
    public static readonly ClientboundResetChatPacket Instance = new();

    public static StreamCodec<FriendlyByteBuf, ClientboundResetChatPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ClientboundResetChatPacket>(Instance);

    private ClientboundResetChatPacket() { }

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundResetChat;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleResetChat(this);
}
