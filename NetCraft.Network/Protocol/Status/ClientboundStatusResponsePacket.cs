namespace NetCraft.Network.Protocol.Status;

//ClientboundStatusResponsePacket 客户端 status 响应包对应原版 net.minecraft.network.protocol.status.ClientboundStatusResponsePacket
//服务端返回 ServerStatus 序列化 JSON 字符串
//简化版用 ToJson/FromJson 替代原版 Codec + RegistryOps
public sealed record ClientboundStatusResponsePacket(ServerStatus Status) : Packet<ClientStatusPacketListener>
{
    //MaxStatusLength status JSON 最大长度 32767
    public const int MaxStatusLength = 32767;

    //StreamCodec 包编解码器对应原版 STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundStatusResponsePacket> StreamCodec { get; } = new StatusResponseCodec();

    //Type 包类型标识
    public PacketType<ClientStatusPacketListener> Type => StatusPacketTypes.ClientboundStatusResponse;

    //Handle 调用处理器的 HandleStatusResponse 方法
    public void Handle(ClientStatusPacketListener handler)
        => handler.HandleStatusResponse(this);

    //StatusResponseCodec 编解码器读写 JSON 字符串
    private sealed class StatusResponseCodec : StreamCodec<FriendlyByteBuf, ClientboundStatusResponsePacket>
    {
        public ClientboundStatusResponsePacket Decode(FriendlyByteBuf buf)
        {
            var json = buf.ReadString(MaxStatusLength);
            var status = ServerStatus.FromJson(json) ?? new ServerStatus();
            return new ClientboundStatusResponsePacket(status);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundStatusResponsePacket value)
            => buf.WriteString(value.Status.ToJson(), MaxStatusLength);
    }
}
