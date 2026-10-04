namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugEntityValuePacket 调试实体值包对应原版 ClientboundDebugEntityValuePacket
//字段 EntityId(int) Update(DebugSubscription.Update<?>)
public sealed record ClientboundDebugEntityValuePacket(int EntityId, object Update) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugEntityValuePacket> StreamCodec { get; } = new DebugEntityValueCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugEntityValue;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugEntityValue(this);

    private sealed class DebugEntityValueCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugEntityValuePacket>
    {
        public ClientboundDebugEntityValuePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugEntityValuePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
