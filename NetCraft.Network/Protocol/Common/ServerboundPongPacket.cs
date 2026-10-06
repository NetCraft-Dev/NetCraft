
namespace NetCraft.Network.Protocol.Common;

//ServerboundPongPacket server-side pong packet, maps to vanilla net.minecraft.network.protocol.common.ServerboundPongPacket
//The client returns the id from the server's ping
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
