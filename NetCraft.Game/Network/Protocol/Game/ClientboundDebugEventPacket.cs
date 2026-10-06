namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugEventPacket debug event packet, maps to vanilla ClientboundDebugEventPacket
//Field: Event(DebugSubscription.Event<?>)
public sealed record ClientboundDebugEventPacket(object Event) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugEventPacket> StreamCodec { get; } = new DebugEventCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugEvent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugEvent(this);

    private sealed class DebugEventCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugEventPacket>
    {
        public ClientboundDebugEventPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugEventPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
