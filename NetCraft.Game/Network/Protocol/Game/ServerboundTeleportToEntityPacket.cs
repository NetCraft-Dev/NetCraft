namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundTeleportToEntityPacket 数据包对应原版 ServerboundTeleportToEntityPacket
//字段 Uuid(UUID)
public sealed record ServerboundTeleportToEntityPacket(Guid Uuid) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundTeleportToEntityPacket> StreamCodec { get; } = new TeleportToEntityCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundTeleportToEntity;

    public void Handle(ServerGamePacketListener handler) => handler.HandleTeleportToEntityPacket(this);

    private sealed class TeleportToEntityCodec : StreamCodec<FriendlyByteBuf, ServerboundTeleportToEntityPacket>
    {
        //旁观者点击实体进行传送时发送 只有目标实体 uuid
        public ServerboundTeleportToEntityPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadUuid());

        public void Encode(FriendlyByteBuf buf, ServerboundTeleportToEntityPacket value)
            => buf.WriteUuid(value.Uuid);
    }
}
