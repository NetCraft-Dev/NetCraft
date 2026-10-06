namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugEntityValuePacket debug entity value packet, maps to vanilla ClientboundDebugEntityValuePacket
//Fields: EntityId(int), Update(DebugSubscription.Update<?>)
public sealed record ClientboundDebugEntityValuePacket(int EntityId, object Update) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugEntityValuePacket> StreamCodec { get; } = new DebugEntityValueCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugEntityValue;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugEntityValue(this);

    private sealed class DebugEntityValueCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugEntityValuePacket>
    {
        public ClientboundDebugEntityValuePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugEntityValuePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
