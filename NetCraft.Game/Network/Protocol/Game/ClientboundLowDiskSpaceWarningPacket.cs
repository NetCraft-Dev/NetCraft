namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundLowDiskSpaceWarningPacket 磁盘空间不足警告包对应原版 ClientboundLowDiskSpaceWarningPacket
//字段 
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
