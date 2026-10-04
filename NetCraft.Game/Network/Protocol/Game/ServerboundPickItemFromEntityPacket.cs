namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPickItemFromEntityPacket 中键选实体包对应原版 ServerboundPickItemFromEntityPacket
//字段 Id(VarInt 实体id) IncludeData(boolean 创造模式是否附带实体数据)
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
