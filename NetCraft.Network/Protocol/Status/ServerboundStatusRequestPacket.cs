namespace NetCraft.Network.Protocol.Status;

//ServerboundStatusRequestPacket 服务端 status 请求包对应原版 net.minecraft.network.protocol.status.ServerboundStatusRequestPacket
//客户端请求服务器状态无 payload 用单例 INSTANCE
//StreamCodec.unit(INSTANCE) 编解码无数据
public sealed record ServerboundStatusRequestPacket : Packet<ServerStatusPacketListener>
{
    //Instance 单例实例
    public static readonly ServerboundStatusRequestPacket Instance = new();

    //StreamCodec 恒定值编解码器对应原版 STREAM_CODEC = StreamCodec.unit(INSTANCE)
    public static StreamCodec<FriendlyByteBuf, ServerboundStatusRequestPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundStatusRequestPacket>(Instance);

    private ServerboundStatusRequestPacket() { }

    //Type 包类型标识
    public PacketType<ServerStatusPacketListener> Type => StatusPacketTypes.ServerboundStatusRequest;

    //Handle 调用处理器的 HandleStatusRequest 方法
    public void Handle(ServerStatusPacketListener handler)
        => handler.HandleStatusRequest(this);
}
