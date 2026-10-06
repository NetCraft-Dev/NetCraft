using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Common;

//ClientboundShowDialogPacket show dialog packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundShowDialogPacket
//Vanilla depends on the Dialog helper to dispatch by Identifier; the simplified form uses Identifier DialogType + byte[] Data to pass through
//The positional parameter uses DialogType to avoid clashing with the Packet.Type interface property
//CONTEXT_FREE_STREAM_CODEC and CONTEXTUAL_STREAM_CODEC are synonymous and share the simplified codec
public sealed record ClientboundShowDialogPacket(Identifier DialogType, byte[] Data) : Packet<ClientCommonPacketListener>
{
    public const int MaxDataLength = 32767;

    //ContextFreeStreamCodec context-free codec, maps to vanilla CONTEXT_FREE_STREAM_CODEC
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
