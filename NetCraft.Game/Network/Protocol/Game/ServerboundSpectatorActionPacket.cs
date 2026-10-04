namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSpectatorActionPacket 数据包对应原版 ServerboundSpectatorActionPacket
//字段 SpectateEntityId(OptionalInt 观察目标实体 id 可为空表示停止观察)
public sealed record ServerboundSpectatorActionPacket(int? SpectateEntityId) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSpectatorActionPacket> StreamCodec { get; } = new SpectatorActionCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSpectatorAction;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSpectatorAction(this);

    private sealed class SpectatorActionCodec : StreamCodec<FriendlyByteBuf, ServerboundSpectatorActionPacket>
    {
        //旁观者切换跟随目标时发送 原版 OptionalInt 先写存在标志再写 varint
        public ServerboundSpectatorActionPacket Decode(FriendlyByteBuf buf)
        {
            if (!buf.ReadBoolean()) return new((int?)null);
            return new(buf.ReadVarInt());
        }

        public void Encode(FriendlyByteBuf buf, ServerboundSpectatorActionPacket value)
        {
            buf.WriteBoolean(value.SpectateEntityId.HasValue);
            if (value.SpectateEntityId.HasValue) buf.WriteVarInt(value.SpectateEntityId.Value);
        }
    }
}
