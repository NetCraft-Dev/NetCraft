using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Cookie;

//ClientboundCookieRequestPacket server cookie request packet, maps to vanilla net.minecraft.network.protocol.cookie.ClientboundCookieRequestPacket
//Contains Identifier key; the server requests the cookie the client stored
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
