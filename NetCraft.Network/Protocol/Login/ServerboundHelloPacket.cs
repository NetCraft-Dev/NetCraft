namespace NetCraft.Network.Protocol.Login;

//ServerboundHelloPacket client login hello packet, maps to vanilla net.minecraft.network.protocol.login.ServerboundHelloPacket
//Contains name, the player name with a 16-character limit, and profileId, the player UUID
public sealed record ServerboundHelloPacket(string Name, Guid ProfileId) : Packet<ServerLoginPacketListener>
{
    //MaxNameLength maximum player name 16 characters
    public const int MaxNameLength = 16;

    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ServerboundHelloPacket> StreamCodec { get; } = new HelloCodec();

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundHello;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleHello(this);

    //HelloCodec codec reading and writing name + UUID
    private sealed class HelloCodec : StreamCodec<FriendlyByteBuf, ServerboundHelloPacket>
    {
        public ServerboundHelloPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxNameLength), buf.ReadUuid());

        public void Encode(FriendlyByteBuf buf, ServerboundHelloPacket value)
        {
            buf.WriteString(value.Name, MaxNameLength);
            buf.WriteUuid(value.ProfileId);
        }
    }
}
