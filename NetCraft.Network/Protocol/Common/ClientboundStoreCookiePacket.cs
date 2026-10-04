using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundStoreCookiePacket 存储 cookie 包对应原版 net.minecraft.network.protocol.common.ClientboundStoreCookiePacket
//含 Identifier key + byte[] payload 服务端请求客户端存储 cookie
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
