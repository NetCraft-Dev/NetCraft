namespace NetCraft.Network.Protocol.Configuration;

//ServerboundFinishConfigurationPacket 客户端通知服务端配置阶段完成
//对应原版 net.minecraft.network.protocol.configuration.ServerboundFinishConfigurationPacket
//无 payload 用单例 INSTANCE
public sealed record ServerboundFinishConfigurationPacket : Packet<ServerConfigurationPacketListener>
{
    public static readonly ServerboundFinishConfigurationPacket Instance = new();

    public static StreamCodec<FriendlyByteBuf, ServerboundFinishConfigurationPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundFinishConfigurationPacket>(Instance);

    private ServerboundFinishConfigurationPacket() { }

    public PacketType<ServerConfigurationPacketListener> Type => ConfigurationPacketTypes.ServerboundFinishConfiguration;

    public void Handle(ServerConfigurationPacketListener handler) => handler.HandleConfigurationFinished(this);
}
