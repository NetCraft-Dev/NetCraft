namespace NetCraft.Network.Protocol.Ping;

//ServerboundPingRequestPacket 服务端 ping 请求包对应原版 net.minecraft.network.protocol.ping.ServerboundPingRequestPacket
//客户端发送含 time 时间戳用于 ping 测量
//Handler 用 ServerPingPacketListener 对齐原版 Packet<? super T> 逆变 Status 与 Play 两个协议都注册它
public sealed record ServerboundPingRequestPacket(long Time) : Packet<ServerPingPacketListener>
{
    //StreamCodec 包编解码器对应原版 STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ServerboundPingRequestPacket> StreamCodec { get; } = new PingRequestCodec();

    //Type 包类型标识
    public PacketType<ServerPingPacketListener> Type => PingPacketTypes.ServerboundPingRequest;

    //Handle 调用处理器的 HandlePingRequest 方法
    public void Handle(ServerPingPacketListener handler)
        => handler.HandlePingRequest(this);

    //PingRequestCodec 编解码器读写 long time
    private sealed class PingRequestCodec : StreamCodec<FriendlyByteBuf, ServerboundPingRequestPacket>
    {
        public ServerboundPingRequestPacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());

        public void Encode(FriendlyByteBuf buf, ServerboundPingRequestPacket value)
            => buf.WriteLong(value.Time);
    }
}
