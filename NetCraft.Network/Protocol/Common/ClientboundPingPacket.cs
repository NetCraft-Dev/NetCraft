
namespace NetCraft.Network.Protocol.Common;

//ClientboundPingPacket client ping packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundPingPacket
//The server sends an int id and the client replies with pong, used for network latency measurement
public sealed record ClientboundPingPacket(int Id) : Packet<ClientCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPingPacket> StreamCodec { get; } = new PingCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundPing;

    public void Handle(ClientCommonPacketListener handler) => handler.HandlePing(this);

    private sealed class PingCodec : StreamCodec<FriendlyByteBuf, ClientboundPingPacket>
    {
        public ClientboundPingPacket Decode(FriendlyByteBuf buf) => new(buf.ReadInt());
        public void Encode(FriendlyByteBuf buf, ClientboundPingPacket value) => buf.WriteInt(value.Id);
    }
}
