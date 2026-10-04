namespace NetCraft.Network.Protocol.Configuration;

//ClientboundFinishConfigurationPacket 服务端通知客户端配置阶段完成
//对应原版 net.minecraft.network.protocol.configuration.ClientboundFinishConfigurationPacket
//无 payload 用单例 INSTANCE
//IsTerminal true 表示完成后切换到 Play 协议
public sealed record ClientboundFinishConfigurationPacket : Packet<ClientConfigurationPacketListener>
{
    //Instance 单例实例
    public static readonly ClientboundFinishConfigurationPacket Instance = new();

    //StreamCodec 恒定值编解码器对应原版 STREAM_CODEC = StreamCodec.unit(INSTANCE)
    public static StreamCodec<FriendlyByteBuf, ClientboundFinishConfigurationPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ClientboundFinishConfigurationPacket>(Instance);

    private ClientboundFinishConfigurationPacket() { }

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundFinishConfiguration;

    //IsTerminal 完成后切换到 Play 协议
    public bool IsTerminal => true;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleConfigurationFinished(this);
}
