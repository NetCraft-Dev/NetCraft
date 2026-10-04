
namespace NetCraft.Network.Protocol.Common;

//ClientboundKeepAlivePacket 客户端心跳包对应原版 net.minecraft.network.protocol.common.ClientboundKeepAlivePacket
//含 long id 心跳标识符服务端发送客户端原样回传用于超时检测
public sealed record ClientboundKeepAlivePacket(long Id) : Packet<ClientCommonPacketListener>
{
    //StreamCodec 包编解码器读写 long id
    public static StreamCodec<FriendlyByteBuf, ClientboundKeepAlivePacket> StreamCodec { get; } = new KeepAliveCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundKeepAlive;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleKeepAlive(this);

    private sealed class KeepAliveCodec : StreamCodec<FriendlyByteBuf, ClientboundKeepAlivePacket>
    {
        public ClientboundKeepAlivePacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());
        public void Encode(FriendlyByteBuf buf, ClientboundKeepAlivePacket value) => buf.WriteLong(value.Id);
    }
}
