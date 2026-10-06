namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundOpenSignEditorPacket open sign editor packet, maps to vanilla ClientboundOpenSignEditorPacket
//Fields: Pos(BlockPos), IsFrontText(boolean)
public sealed record ClientboundOpenSignEditorPacket(object Pos, bool IsFrontText) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundOpenSignEditorPacket> StreamCodec { get; } = new OpenSignEditorCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundOpenSignEditor;

    public void Handle(ClientGamePacketListener handler) => handler.HandleOpenSignEditor(this);

    private sealed class OpenSignEditorCodec : StreamCodec<FriendlyByteBuf, ClientboundOpenSignEditorPacket>
    {
        public ClientboundOpenSignEditorPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundOpenSignEditorPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
