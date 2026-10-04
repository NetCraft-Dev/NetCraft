namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerCombatEnterPacket 战斗开始包对应原版 ClientboundPlayerCombatEnterPacket
//字段 
public sealed record ClientboundPlayerCombatEnterPacket() : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatEnterPacket> StreamCodec { get; } = new PlayerCombatEnterCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerCombatEnter;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerCombatEnter(this);

    private sealed class PlayerCombatEnterCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatEnterPacket>
    {
        public ClientboundPlayerCombatEnterPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerCombatEnterPacket value)
            { }
    }
}
