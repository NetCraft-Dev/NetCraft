
namespace NetCraft.Network.Protocol.Common;

//ServerboundPongPacket 服务端 pong 包对应原版 net.minecraft.network.protocol.common.ServerboundPongPacket
//客户端回传服务端 ping 的 id
public sealed record ServerboundPongPacket(int Id) : Packet<ServerCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPongPacket> StreamCodec { get; } = new PongCodec();

    public PacketType<ServerCommonPacketListener> Type => CommonPacketTypes.ServerboundPong;

    public void Handle(ServerCommonPacketListener handler) => handler.HandlePong(this);

    private sealed class PongCodec : StreamCodec<FriendlyByteBuf, ServerboundPongPacket>
    {
        public ServerboundPongPacket Decode(FriendlyByteBuf buf) => new(buf.ReadInt());
        public void Encode(FriendlyByteBuf buf, ServerboundPongPacket value) => buf.WriteInt(value.Id);
    }
}
