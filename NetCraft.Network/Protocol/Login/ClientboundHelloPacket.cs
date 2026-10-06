namespace NetCraft.Network.Protocol.Login;

//ClientboundHelloPacket server encryption handshake packet, maps to vanilla net.minecraft.network.protocol.login.ClientboundHelloPacket
//Contains serverId + publicKey + challenge + shouldAuthenticate
//The simplified form does not parse PublicKey and keeps it as byte[]
public sealed record ClientboundHelloPacket(
    string ServerId,
    byte[] PublicKey,
    byte[] Challenge,
    bool ShouldAuthenticate) : Packet<ClientLoginPacketListener>
{
    //MaxServerIdLength maximum serverId length 20 characters
    public const int MaxServerIdLength = 20;

    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ClientboundHelloPacket> StreamCodec { get; } = new HelloCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundHello;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleHello(this);

    //HelloCodec codec reading and writing serverId + publicKey + challenge + shouldAuthenticate
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
