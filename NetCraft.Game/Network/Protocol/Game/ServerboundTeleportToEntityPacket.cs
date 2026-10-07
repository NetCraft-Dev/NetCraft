namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundTeleportToEntityPacket teleport to entity packet, maps to vanilla ServerboundTeleportToEntityPacket
//Field: Uuid(UUID)
public sealed record ServerboundTeleportToEntityPacket(Guid Uuid) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundTeleportToEntityPacket> StreamCodec { get; } = new TeleportToEntityCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundTeleportToEntity;

    public void Handle(ServerGamePacketListener handler) => handler.HandleTeleportToEntityPacket(this);

    private sealed class TeleportToEntityCodec : StreamCodec<FriendlyByteBuf, ServerboundTeleportToEntityPacket>
    {
        //Sent when a spectator clicks an entity to teleport; carries only the target entity uuid
        public ServerboundTeleportToEntityPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadUuid());

        public void Encode(FriendlyByteBuf buf, ServerboundTeleportToEntityPacket value)
            => buf.WriteUuid(value.Uuid);
    }
}
