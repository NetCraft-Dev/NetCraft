namespace NetCraft.Network.Protocol.Ping;

//ClientboundPongResponsePacket 客户端 pong 响应包对应原版 net.minecraft.network.protocol.ping.ClientboundPongResponsePacket
//服务端回传 ping 请求的 time 时间戳客户端计算往返延迟
//Handler 用 ClientPongPacketListener 对齐原版 Packet<? super T> 逆变 Status 与 Play 两个协议都注册它
public sealed record ClientboundPongResponsePacket(long Time) : Packet<ClientPongPacketListener>
{
    //StreamCodec 包编解码器对应原版 STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundPongResponsePacket> StreamCodec { get; } = new PongResponseCodec();

    //Type 包类型标识 只作标识不再决定网络 ID
    //同一包类在 Status 与 Play 注册的 ID 不同 编码时由当前协议表按包类反查
    public PacketType<ClientPongPacketListener> Type => PingPacketTypes.ClientboundPongResponse;

    //Handle 调用处理器的 HandlePongResponse 方法
    public void Handle(ClientPongPacketListener handler)
        => handler.HandlePongResponse(this);

    //PongResponseCodec 编解码器读写 long time
    private sealed class PongResponseCodec : StreamCodec<FriendlyByteBuf, ClientboundPongResponsePacket>
    {
        public ClientboundPongResponsePacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());

        public void Encode(FriendlyByteBuf buf, ClientboundPongResponsePacket value)
            => buf.WriteLong(value.Time);
    }
}
