namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerCombatKillPacket 战斗致死包对应原版 ClientboundPlayerCombatKillPacket
//字段 PlayerId(int) Message(Component)
public sealed record ClientboundPlayerCombatKillPacket(int PlayerId, Component Message) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatKillPacket> StreamCodec { get; } = new PlayerCombatKillCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerCombatKill;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerCombatKill(this);

    private sealed class PlayerCombatKillCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatKillPacket>
    {
        public ClientboundPlayerCombatKillPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), ComponentSerialization.StreamCodec.Decode(buf));

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerCombatKillPacket value)
        {
            buf.WriteVarInt(value.PlayerId);
            ComponentSerialization.StreamCodec.Encode(buf, value.Message);
        }
    }
}
