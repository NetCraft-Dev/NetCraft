namespace NetCraft.Network.Protocol.Configuration;

//ClientboundResetChatPacket 服务端通知客户端重置聊天状态
//对应原版 net.minecraft.network.protocol.configuration.ClientboundResetChatPacket
//无 payload 用单例 INSTANCE
public sealed record ClientboundResetChatPacket : Packet<ClientConfigurationPacketListener>
{
    public static readonly ClientboundResetChatPacket Instance = new();

    public static StreamCodec<FriendlyByteBuf, ClientboundResetChatPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ClientboundResetChatPacket>(Instance);

    private ClientboundResetChatPacket() { }

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundResetChat;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleResetChat(this);
}
