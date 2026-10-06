
namespace NetCraft.Network.Protocol.Common;

//ClientboundKeepAlivePacket client keep-alive packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundKeepAlivePacket
//Contains a long id keep-alive identifier; the server sends it and the client returns it unchanged for timeout detection
public sealed record ClientboundKeepAlivePacket(long Id) : Packet<ClientCommonPacketListener>
{
    //StreamCodec packet codec reading and writing the long id
    public static StreamCodec<FriendlyByteBuf, ClientboundKeepAlivePacket> StreamCodec { get; } = new KeepAliveCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundKeepAlive;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleKeepAlive(this);

    private sealed class KeepAliveCodec : StreamCodec<FriendlyByteBuf, ClientboundKeepAlivePacket>
    {
        public ClientboundKeepAlivePacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());
        public void Encode(FriendlyByteBuf buf, ClientboundKeepAlivePacket value) => buf.WriteLong(value.Id);
    }
}
