
namespace NetCraft.Network.Protocol.Common;

//ClientboundClearDialogPacket 清除对话框包对应原版 net.minecraft.network.protocol.common.ClientboundClearDialogPacket
//含 int id 服务端请求客户端关闭指定对话框
public sealed record ClientboundClearDialogPacket(int Id) : Packet<ClientCommonPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundClearDialogPacket> StreamCodec { get; } = new ClearDialogCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundClearDialog;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleClearDialog(this);

    private sealed class ClearDialogCodec : StreamCodec<FriendlyByteBuf, ClientboundClearDialogPacket>
    {
        public ClientboundClearDialogPacket Decode(FriendlyByteBuf buf) => new(buf.ReadVarInt());
        public void Encode(FriendlyByteBuf buf, ClientboundClearDialogPacket value) => buf.WriteVarInt(value.Id);
    }
}
