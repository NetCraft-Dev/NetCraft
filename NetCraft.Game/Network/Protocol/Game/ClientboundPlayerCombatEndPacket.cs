namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerCombatEndPacket 战斗结束包对应原版 ClientboundPlayerCombatEndPacket
//字段 Duration(int)
public sealed record ClientboundPlayerCombatEndPacket(int Duration) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatEndPacket> StreamCodec { get; } = new PlayerCombatEndCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerCombatEnd;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerCombatEnd(this);

    private sealed class PlayerCombatEndCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatEndPacket>
    {
        public ClientboundPlayerCombatEndPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerCombatEndPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
