namespace NetCraft.Network.Protocol.Handshake;

//ClientIntentionPacket 客户端握手包对应原版 net.minecraft.network.protocol.handshake.ClientIntentionPacket
//客户端发起握手时发送含协议版本主机端口和意图
//实现 Packet<ServerHandshakePacketListener> 接口
//IsTerminal 为 true 表示握手包后协议切换不可继续在 HANDSHAKE 状态
public sealed record ClientIntentionPacket(
    int ProtocolVersion,
    string HostName,
    int Port,
    ClientIntent Intention) : Packet<ServerHandshakePacketListener>
{
    //MaxHostLength 主机名最大长度 255 字节
    public const int MaxHostLength = 255;

    //StreamCodec 包编解码器对应原版 STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientIntentionPacket> StreamCodec { get; } = new ClientIntentionCodec();

    //Type 包类型标识
    public PacketType<ServerHandshakePacketListener> Type => HandshakePacketTypes.ClientIntention;

    //IsTerminal 握手包终止 HANDSHAKE 状态切换到 STATUS 或 LOGIN
    public bool IsTerminal => true;

    //Handle 调用处理器的 handleIntention 方法
    public void Handle(ServerHandshakePacketListener handler)
        => handler.HandleIntention(this);

    //ClientIntentionCodec 编解码器内部实现
    //读 VarInt 协议版本 + UTF-8 主机名 + ushort 端口 + VarInt 意图 ID
    private sealed class ClientIntentionCodec : StreamCodec<FriendlyByteBuf, ClientIntentionPacket>
    {
        public ClientIntentionPacket Decode(FriendlyByteBuf buf)
        {
            int protocolVersion = buf.ReadVarInt();
            string hostName = buf.ReadString(MaxHostLength);
            int port = (ushort)buf.ReadShort();
            var intention = ClientIntentExtensions.ById(buf.ReadVarInt());
            return new ClientIntentionPacket(protocolVersion, hostName, port, intention);
        }

        public void Encode(FriendlyByteBuf buf, ClientIntentionPacket value)
        {
            buf.WriteVarInt(value.ProtocolVersion);
            buf.WriteString(value.HostName, MaxHostLength);
            buf.WriteShort((short)value.Port);
            buf.WriteVarInt(value.Intention.Id());
        }
    }
}
