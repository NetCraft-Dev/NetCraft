namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugSamplePacket debug sample packet, maps to vanilla ClientboundDebugSamplePacket
//Field: DebugSampleType(RemoteDebugSampleType)
public sealed record ClientboundDebugSamplePacket(object DebugSampleType) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugSamplePacket> StreamCodec { get; } = new DebugSampleCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugSample;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugSample(this);

    private sealed class DebugSampleCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugSamplePacket>
    {
        public ClientboundDebugSamplePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugSamplePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
