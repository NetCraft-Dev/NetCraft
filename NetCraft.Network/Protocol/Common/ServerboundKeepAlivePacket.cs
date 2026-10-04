
namespace NetCraft.Network.Protocol.Common;

//ServerboundKeepAlivePacket 服务端心跳包对应原版 net.minecraft.network.protocol.common.ServerboundKeepAlivePacket
//客户端回传服务端发的 keep alive id
public sealed record ServerboundKeepAlivePacket(long Id) : Packet<ServerCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundKeepAlivePacket> StreamCodec { get; } = new KeepAliveCodec();

    public PacketType<ServerCommonPacketListener> Type => CommonPacketTypes.ServerboundKeepAlive;

    public void Handle(ServerCommonPacketListener handler) => handler.HandleKeepAlive(this);

    private sealed class KeepAliveCodec : StreamCodec<FriendlyByteBuf, ServerboundKeepAlivePacket>
    {
        public ServerboundKeepAlivePacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());
        public void Encode(FriendlyByteBuf buf, ServerboundKeepAlivePacket value) => buf.WriteLong(value.Id);
    }
}
