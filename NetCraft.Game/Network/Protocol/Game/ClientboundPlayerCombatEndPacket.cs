namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerCombatEndPacket combat end packet, maps to vanilla ClientboundPlayerCombatEndPacket
//Field: Duration(int)
public sealed record ClientboundPlayerCombatEndPacket(int Duration) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatEndPacket> StreamCodec { get; } = new PlayerCombatEndCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerCombatEnd;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerCombatEnd(this);

    private sealed class PlayerCombatEndCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerCombatEndPacket>
    {
        public ClientboundPlayerCombatEndPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerCombatEndPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
