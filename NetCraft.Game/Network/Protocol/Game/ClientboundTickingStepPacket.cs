namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTickingStepPacket Tick 步进包对应原版 ClientboundTickingStepPacket
//字段 TickSteps(int)
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
