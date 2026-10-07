namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTickingStatePacket tick state packet, maps to vanilla ClientboundTickingStatePacket
//Fields: TickRate(float), IsFrozen(boolean)
public sealed record ClientboundTickingStatePacket(float TickRate, bool IsFrozen) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTickingStatePacket> StreamCodec { get; } = new TickingStateCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTickingState;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTickingState(this);

    private sealed class TickingStateCodec : StreamCodec<FriendlyByteBuf, ClientboundTickingStatePacket>
    {
        public ClientboundTickingStatePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadFloat(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ClientboundTickingStatePacket value)
        {
            buf.WriteFloat(value.TickRate);
            buf.WriteBoolean(value.IsFrozen);
        }
    }
}
