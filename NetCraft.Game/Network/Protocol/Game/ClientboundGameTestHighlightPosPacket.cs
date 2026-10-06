namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameTestHighlightPosPacket game test highlight pos packet, maps to vanilla ClientboundGameTestHighlightPosPacket
//Fields: AbsolutePos(BlockPos), RelativePos(BlockPos)
public sealed record ClientboundGameTestHighlightPosPacket(object AbsolutePos, object RelativePos) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundGameTestHighlightPosPacket> StreamCodec { get; } = new GameTestHighlightPosCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundGameTestHighlightPos;

    public void Handle(ClientGamePacketListener handler) => handler.HandleGameTestHighlightPos(this);

    private sealed class GameTestHighlightPosCodec : StreamCodec<FriendlyByteBuf, ClientboundGameTestHighlightPosPacket>
    {
        public ClientboundGameTestHighlightPosPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundGameTestHighlightPosPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
