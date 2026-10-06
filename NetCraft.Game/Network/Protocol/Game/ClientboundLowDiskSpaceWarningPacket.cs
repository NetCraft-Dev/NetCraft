namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLowDiskSpaceWarningPacket low disk space warning packet, maps to vanilla ClientboundLowDiskSpaceWarningPacket
//Fields:
public sealed record ClientboundLowDiskSpaceWarningPacket() : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundLowDiskSpaceWarningPacket> StreamCodec { get; } = new LowDiskSpaceWarningCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundLowDiskSpaceWarning;

    public void Handle(ClientGamePacketListener handler) => handler.HandleLowDiskSpaceWarning(this);

    private sealed class LowDiskSpaceWarningCodec : StreamCodec<FriendlyByteBuf, ClientboundLowDiskSpaceWarningPacket>
    {
        public ClientboundLowDiskSpaceWarningPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ClientboundLowDiskSpaceWarningPacket value)
            { }
    }
}
