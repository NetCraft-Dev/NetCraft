namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlayerInfoRemovePacket 玩家信息移除包对应原版 ClientboundPlayerInfoRemovePacket
//字段 ProfileIds(List<UUID>) 原版 writeCollection 写 VarInt 数量 + 每个 UUID 16 字节
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
