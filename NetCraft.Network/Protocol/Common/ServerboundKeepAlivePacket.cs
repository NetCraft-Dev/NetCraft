
namespace NetCraft.Network.Protocol.Common;

//ServerboundKeepAlivePacket server-side keep-alive packet, maps to vanilla net.minecraft.network.protocol.common.ServerboundKeepAlivePacket
//The client returns the keep alive id sent by the server
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
