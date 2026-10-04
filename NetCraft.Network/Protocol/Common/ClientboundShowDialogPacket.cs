using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundShowDialogPacket 显示对话框包对应原版 net.minecraft.network.protocol.common.ClientboundShowDialogPacket
//原版依赖 Dialog 辅助类型按 Identifier 分发简化版用 Identifier DialogType + byte[] Data 透传
//位置参数用 DialogType 避免与 Packet.Type 接口属性冲突
//CONTEXT_FREE_STREAM_CODEC 和 CONTEXTUAL_STREAM_CODEC 同义共享简化编解码
public sealed record ClientboundShowDialogPacket(Identifier DialogType, byte[] Data) : Packet<ClientCommonPacketListener>
{
    public const int MaxDataLength = 32767;

    //ContextFreeStreamCodec 上下文无关编解码器对应原版 CONTEXT_FREE_STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundShowDialogPacket> ContextFreeStreamCodec { get; } = new ShowDialogCodec();

    public PacketType<ClientCommonPacketListener> Type => CommonPacketTypes.ClientboundShowDialog;

    public void Handle(ClientCommonPacketListener handler) => handler.HandleShowDialog(this);

    private sealed class ShowDialogCodec : StreamCodec<FriendlyByteBuf, ClientboundShowDialogPacket>
    {
        public ClientboundShowDialogPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadByteArray(MaxDataLength));

        public void Encode(FriendlyByteBuf buf, ClientboundShowDialogPacket value)
        {
            buf.WriteIdentifier(value.DialogType);
            buf.WriteByteArray(value.Data, MaxDataLength);
        }
    }
}
