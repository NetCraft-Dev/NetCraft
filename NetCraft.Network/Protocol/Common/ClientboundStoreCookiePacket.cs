using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundStoreCookiePacket store cookie packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundStoreCookiePacket
//Contains Identifier key + byte[] payload; the server asks the client to store a cookie
public sealed record ClientboundStoreCookiePacket(Identifier Key, byte[] Payload) : Packet<ClientCommonPacketListener>
{
    public const int MaxPayloadLength = 1024;

    public static StreamCodec<FriendlyByteBuf, ClientboundStoreCookiePacket> StreamCodec { get; } = new StoreCookieCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundStoreCookie;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleStoreCookie(this);

    private sealed class StoreCookieCodec : StreamCodec<FriendlyByteBuf, ClientboundStoreCookiePacket>
    {
        public ClientboundStoreCookiePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxPayloadLength));

        public void Encode(FriendlyByteBuf buf, ClientboundStoreCookiePacket value)
        {
            buf.WriteIdentifier(value.Key);
            buf.WriteByteArray(value.Payload, MaxPayloadLength);
        }
    }
}
