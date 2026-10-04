namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundGameTestHighlightPosPacket 测试高亮位置包对应原版 ClientboundGameTestHighlightPosPacket
//字段 AbsolutePos(BlockPos) RelativePos(BlockPos)
public sealed record ClientboundGameTestHighlightPosPacket(object AbsolutePos, object RelativePos) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundGameTestHighlightPosPacket> StreamCodec { get; } = new GameTestHighlightPosCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundGameTestHighlightPos;

    public void Handle(ClientGamePacketListener handler) => handler.HandleGameTestHighlightPos(this);

    private sealed class GameTestHighlightPosCodec : StreamCodec<FriendlyByteBuf, ClientboundGameTestHighlightPosPacket>
    {
        public ClientboundGameTestHighlightPosPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundGameTestHighlightPosPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
