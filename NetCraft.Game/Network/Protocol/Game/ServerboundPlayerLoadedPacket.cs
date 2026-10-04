namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlayerLoadedPacket 数据包对应原版 ServerboundPlayerLoadedPacket
//字段 
public sealed record ServerboundPlayerLoadedPacket() : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlayerLoadedPacket> StreamCodec { get; } = new PlayerLoadedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlayerLoaded;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAcceptPlayerLoad(this);

    private sealed class PlayerLoadedCodec : StreamCodec<FriendlyByteBuf, ServerboundPlayerLoadedPacket>
    {
        public ServerboundPlayerLoadedPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ServerboundPlayerLoadedPacket value)
            { }
    }
}
