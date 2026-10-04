namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugBlockValuePacket 调试方块值包对应原版 ClientboundDebugBlockValuePacket
//字段 BlockPos(BlockPos) Update(DebugSubscription.Update<?>)
public sealed record ClientboundDebugBlockValuePacket(object BlockPos, object Update) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugBlockValuePacket> StreamCodec { get; } = new DebugBlockValueCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugBlockValue;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugBlockValue(this);

    private sealed class DebugBlockValueCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugBlockValuePacket>
    {
        public ClientboundDebugBlockValuePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugBlockValuePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
