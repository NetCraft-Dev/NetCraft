namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundDebugSamplePacket 调试采样包对应原版 ClientboundDebugSamplePacket
//字段 DebugSampleType(RemoteDebugSampleType)
public sealed record ClientboundDebugSamplePacket(object DebugSampleType) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundDebugSamplePacket> StreamCodec { get; } = new DebugSampleCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundDebugSample;

    public void Handle(ClientGamePacketListener handler) => handler.HandleDebugSample(this);

    private sealed class DebugSampleCodec : StreamCodec<FriendlyByteBuf, ClientboundDebugSamplePacket>
    {
        public ClientboundDebugSamplePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundDebugSamplePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
