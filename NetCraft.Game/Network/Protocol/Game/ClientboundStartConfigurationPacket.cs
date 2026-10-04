namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundStartConfigurationPacket 进入配置阶段包对应原版 ClientboundStartConfigurationPacket
//字段 
public sealed record ClientboundStartConfigurationPacket() : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundStartConfigurationPacket> StreamCodec { get; } = new StartConfigurationCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundStartConfiguration;

    public void Handle(ClientGamePacketListener handler) => handler.HandleConfigurationStart(this);

    private sealed class StartConfigurationCodec : StreamCodec<FriendlyByteBuf, ClientboundStartConfigurationPacket>
    {
        public ClientboundStartConfigurationPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ClientboundStartConfigurationPacket value)
            { }
    }
}
