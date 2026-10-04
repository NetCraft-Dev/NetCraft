namespace NetCraft.Network.Protocol.Login;

//ServerboundHelloPacket 客户端登录 hello 包对应原版 net.minecraft.network.protocol.login.ServerboundHelloPacket
//含 name 玩家名 16 字符上限和 profileId 玩家 UUID
public sealed record ServerboundHelloPacket(string Name, Guid ProfileId) : Packet<ServerLoginPacketListener>
{
    //MaxNameLength 玩家名最大 16 字符
    public const int MaxNameLength = 16;

    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ServerboundHelloPacket> StreamCodec { get; } = new HelloCodec();

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundHello;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleHello(this);

    //HelloCodec 编解码器读写 name + UUID
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
