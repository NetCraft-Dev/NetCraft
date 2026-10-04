namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundOpenSignEditorPacket 打开告示牌编辑包对应原版 ClientboundOpenSignEditorPacket
//字段 Pos(BlockPos) IsFrontText(boolean)
public sealed record ClientboundOpenSignEditorPacket(object Pos, bool IsFrontText) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundOpenSignEditorPacket> StreamCodec { get; } = new OpenSignEditorCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundOpenSignEditor;

    public void Handle(ClientGamePacketListener handler) => handler.HandleOpenSignEditor(this);

    private sealed class OpenSignEditorCodec : StreamCodec<FriendlyByteBuf, ClientboundOpenSignEditorPacket>
    {
        public ClientboundOpenSignEditorPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundOpenSignEditorPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
