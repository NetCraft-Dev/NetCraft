namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerInfoRemovePacket player info remove packet, maps to vanilla ClientboundPlayerInfoRemovePacket
//Field: ProfileIds(List<UUID>); vanilla writeCollection writes a VarInt count + 16 bytes per UUID
public sealed record ClientboundPlayerInfoRemovePacket(IReadOnlyList<Guid> ProfileIds) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoRemovePacket> StreamCodec { get; } = new PlayerInfoRemoveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlayerInfoRemove;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlayerInfoRemove(this);

    private sealed class PlayerInfoRemoveCodec : StreamCodec<FriendlyByteBuf, ClientboundPlayerInfoRemovePacket>
    {
        public ClientboundPlayerInfoRemovePacket Decode(FriendlyByteBuf buf)
        {
            var count = buf.ReadVarInt();
            var ids = new Guid[count];
            for (var i = 0; i < count; i++) ids[i] = buf.ReadUuid();
            return new(ids);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundPlayerInfoRemovePacket value)
        {
            buf.WriteVarInt(value.ProfileIds.Count);
            foreach (var id in value.ProfileIds) buf.WriteUuid(id);
        }
    }
}
