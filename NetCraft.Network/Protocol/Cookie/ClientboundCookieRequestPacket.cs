using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Cookie;

//ClientboundCookieRequestPacket 服务端 cookie 请求包对应原版 net.minecraft.network.protocol.cookie.ClientboundCookieRequestPacket
//含 Identifier key 服务端请求客户端存储的 cookie
public sealed record ClientboundCookieRequestPacket(Identifier Key) : Packet<ClientCookiePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCookieRequestPacket> StreamCodec { get; } = new CookieRequestCodec();

    public PacketType<ClientCookiePacketListener> Type => CookiePacketTypes.ClientboundCookieRequest;

    public void Handle(ClientCookiePacketListener handler) => handler.HandleCookieRequest(this);

    private sealed class CookieRequestCodec : StreamCodec<FriendlyByteBuf, ClientboundCookieRequestPacket>
    {
        public ClientboundCookieRequestPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier());

        public void Encode(FriendlyByteBuf buf, ClientboundCookieRequestPacket value)
            => buf.WriteIdentifier(value.Key);
    }
}
