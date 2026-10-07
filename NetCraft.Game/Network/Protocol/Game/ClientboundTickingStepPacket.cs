namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTickingStepPacket tick step packet, maps to vanilla ClientboundTickingStepPacket
//Field: TickSteps(int)
public sealed record ClientboundTickingStepPacket(int TickSteps) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTickingStepPacket> StreamCodec { get; } = new TickingStepCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTickingStep;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTickingStep(this);

    private sealed class TickingStepCodec : StreamCodec<FriendlyByteBuf, ClientboundTickingStepPacket>
    {
        public ClientboundTickingStepPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundTickingStepPacket value)
            => buf.WriteVarInt(value.TickSteps);
    }
}
