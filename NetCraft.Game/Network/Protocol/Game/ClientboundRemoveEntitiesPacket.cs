namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRemoveEntitiesPacket 移除实体包对应原版 ClientboundRemoveEntitiesPacket
//字段 entityIds VarInt 长度前缀的 int 数组
public sealed record ClientboundRemoveEntitiesPacket(int[] EntityIds) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRemoveEntitiesPacket> StreamCodec { get; } = new RemoveEntitiesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRemoveEntities;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRemoveEntities(this);

    private sealed class RemoveEntitiesCodec : StreamCodec<FriendlyByteBuf, ClientboundRemoveEntitiesPacket>
    {
        public ClientboundRemoveEntitiesPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIntIdList());

        public void Encode(FriendlyByteBuf buf, ClientboundRemoveEntitiesPacket value)
            => buf.WriteIntIdList(value.EntityIds);
    }
}
