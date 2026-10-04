
namespace NetCraft.Network.Protocol.Common;

//ClientboundTransferPacket 传输包对应原版 net.minecraft.network.protocol.common.ClientboundTransferPacket
//含 string host + int port 服务端请求客户端转移到另一服务器
public sealed record ClientboundTransferPacket(string Host, int Port) : Packet<ClientCommonPacketListener>
{
    public const int MaxHostLength = 255;
    public const int MaxPort = 65535;

    public static StreamCodec<FriendlyByteBuf, ClientboundTransferPacket> StreamCodec { get; } = new TransferCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundTransfer;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleTransfer(this);

    private sealed class TransferCodec : StreamCodec<FriendlyByteBuf, ClientboundTransferPacket>
    {
        public ClientboundTransferPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxHostLength), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundTransferPacket value)
        {
            buf.WriteString(value.Host, MaxHostLength);
            buf.WriteVarInt(value.Port);
        }
    }
}
