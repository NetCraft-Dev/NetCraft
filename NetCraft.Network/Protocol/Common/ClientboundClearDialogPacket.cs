
namespace NetCraft.Network.Protocol.Common;

//ClientboundClearDialogPacket clear dialog packet, maps to vanilla net.minecraft.network.protocol.common.ClientboundClearDialogPacket
//Contains an int id; the server asks the client to close the given dialog
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
