namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPickItemFromEntityPacket middle-click pick entity packet, maps to vanilla ServerboundPickItemFromEntityPacket
//Fields: Id(VarInt entity id), IncludeData(boolean, whether to include entity data in creative mode)
public sealed record ServerboundPickItemFromEntityPacket(int Id, bool IncludeData) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPickItemFromEntityPacket> StreamCodec { get; } = new PickItemFromEntityCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPickItemFromEntity;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePickItemFromEntity(this);

    private sealed class PickItemFromEntityCodec : StreamCodec<FriendlyByteBuf, ServerboundPickItemFromEntityPacket>
    {
        public ServerboundPickItemFromEntityPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundPickItemFromEntityPacket value)
        {
            buf.WriteVarInt(value.Id);
            buf.WriteBoolean(value.IncludeData);
        }
    }
}
