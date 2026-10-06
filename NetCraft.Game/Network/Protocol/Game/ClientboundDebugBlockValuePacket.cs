namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugBlockValuePacket debug block value packet, maps to vanilla ClientboundDebugBlockValuePacket
//Fields: BlockPos(BlockPos), Update(DebugSubscription.Update<?>)
public sealed record ClientboundDebugBlockValuePacket(object BlockPos, object Update) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugBlockValuePacket> StreamCodec { get; } = new DebugBlockValueCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugBlockValue;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugBlockValue(this);

    private sealed class DebugBlockValueCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugBlockValuePacket>
    {
        public ClientboundDebugBlockValuePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugBlockValuePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
