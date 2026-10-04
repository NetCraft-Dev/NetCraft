using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Cookie;

//ServerboundCookieResponsePacket 客户端 cookie 响应包对应原版 net.minecraft.network.protocol.cookie.ServerboundCookieResponsePacket
//含 Identifier key 和 byte[] payload 客户端回传服务端请求的 cookie 值
public sealed record ServerboundCookieResponsePacket(Identifier Key, byte[] Payload) : Packet<ServerCookiePacketListener>
{
    //MaxPayloadLength payload 最大长度 1024 对齐原版 MAX_PAYLOAD_LENGTH
    public const int MaxPayloadLength = 1024;

    public static StreamCodec<FriendlyByteBuf, ServerboundCookieResponsePacket> StreamCodec { get; } = new CookieResponseCodec();

    public PacketType<ServerCookiePacketListener> Type => CookiePacketTypes.ServerboundCookieResponse;

    public void Handle(ServerCookiePacketListener handler) => handler.HandleCookieResponse(this);

    private sealed class CookieResponseCodec : StreamCodec<FriendlyByteBuf, ServerboundCookieResponsePacket>
    {
        public ServerboundCookieResponsePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxPayloadLength));

        public void Encode(FriendlyByteBuf buf, ServerboundCookieResponsePacket value)
        {
            buf.WriteIdentifier(value.Key);
            buf.WriteByteArray(value.Payload, MaxPayloadLength);
        }
    }
}
