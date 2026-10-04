
namespace NetCraft.Network.Protocol.Common;

//ClientboundPingPacket 客户端 ping 包对应原版 net.minecraft.network.protocol.common.ClientboundPingPacket
//服务端发送 int id 客户端回 pong 用于网络延迟测量
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
