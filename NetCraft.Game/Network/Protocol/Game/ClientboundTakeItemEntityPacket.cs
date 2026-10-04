namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTakeItemEntityPacket 拾取物品包对应原版 ClientboundTakeItemEntityPacket
//字段 ItemId(int) PlayerId(int) Amount(int)
public sealed record ClientboundTakeItemEntityPacket(int ItemId, int PlayerId, int Amount) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTakeItemEntityPacket> StreamCodec { get; } = new TakeItemEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTakeItemEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTakeItemEntity(this);

    private sealed class TakeItemEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundTakeItemEntityPacket>
    {
        //原版顺序 writeVarInt itemId -> playerId -> amount 三个都是 VarInt
        public ClientboundTakeItemEntityPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt(), buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundTakeItemEntityPacket value)
        {
            buf.WriteVarInt(value.ItemId);
            buf.WriteVarInt(value.PlayerId);
            buf.WriteVarInt(value.Amount);
        }
    }
}
