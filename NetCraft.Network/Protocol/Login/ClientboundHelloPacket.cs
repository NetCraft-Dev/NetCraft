namespace NetCraft.Network.Protocol.Login;

//ClientboundHelloPacket 服务端加密握手包对应原版 net.minecraft.network.protocol.login.ClientboundHelloPacket
//含 serverId + publicKey + challenge + shouldAuthenticate
//简化版不解析 PublicKey 直接保留 byte[]
public sealed record ClientboundHelloPacket(
    string ServerId,
    byte[] PublicKey,
    byte[] Challenge,
    bool ShouldAuthenticate) : Packet<ClientLoginPacketListener>
{
    //MaxServerIdLength serverId 最大 20 字符
    public const int MaxServerIdLength = 20;

    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ClientboundHelloPacket> StreamCodec { get; } = new HelloCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundHello;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleHello(this);

    //HelloCodec 编解码器读写 serverId + publicKey + challenge + shouldAuthenticate
    private sealed class HelloCodec : StreamCodec<FriendlyByteBuf, ClientboundHelloPacket>
    {
        public ClientboundHelloPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxServerIdLength),
                buf.ReadByteArray(),
                buf.ReadByteArray(),
                buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ClientboundHelloPacket value)
        {
            buf.WriteString(value.ServerId, MaxServerIdLength);
            buf.WriteByteArray(value.PublicKey);
            buf.WriteByteArray(value.Challenge);
            buf.WriteBoolean(value.ShouldAuthenticate);
        }
    }
}
