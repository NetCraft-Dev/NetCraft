namespace NetCraft.Network.Protocol.Configuration;

//ServerboundAcceptCodeOfConductPacket 客户端确认接受服务端行为准则
//对应原版 net.minecraft.network.protocol.configuration.ServerboundAcceptCodeOfConductPacket
//无 payload 用单例 INSTANCE
public sealed record ServerboundAcceptCodeOfConductPacket : Packet<ServerConfigurationPacketListener>
{
    public static readonly ServerboundAcceptCodeOfConductPacket Instance = new();

    public static StreamCodec<FriendlyByteBuf, ServerboundAcceptCodeOfConductPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundAcceptCodeOfConductPacket>(Instance);

    private ServerboundAcceptCodeOfConductPacket() { }

    public PacketType<ServerConfigurationPacketListener> Type => ConfigurationPacketTypes.ServerboundAcceptCodeOfConduct;

    public void Handle(ServerConfigurationPacketListener handler) => handler.HandleAcceptCodeOfConduct(this);
}
