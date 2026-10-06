using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Cookie;

//ServerboundCookieResponsePacket client cookie response packet, maps to vanilla net.minecraft.network.protocol.cookie.ServerboundCookieResponsePacket
//Contains Identifier key and byte[] payload; the client returns the cookie value the server requested
public sealed record ServerboundCookieResponsePacket(Identifier Key, byte[] Payload) : Packet<ServerCookiePacketListener>
{
    //MaxPayloadLength maximum payload length 1024, aligns with vanilla MAX_PAYLOAD_LENGTH
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
