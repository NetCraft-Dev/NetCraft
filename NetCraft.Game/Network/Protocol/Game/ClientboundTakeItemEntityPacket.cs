namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundTakeItemEntityPacket take item entity packet, maps to vanilla ClientboundTakeItemEntityPacket
//Fields: ItemId(int), PlayerId(int), Amount(int)
public sealed record ClientboundTakeItemEntityPacket(int ItemId, int PlayerId, int Amount) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundTakeItemEntityPacket> StreamCodec { get; } = new TakeItemEntityCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundTakeItemEntity;

    public void Handle(ClientGamePacketListener handler) => handler.HandleTakeItemEntity(this);

    private sealed class TakeItemEntityCodec : StreamCodec<FriendlyByteBuf, ClientboundTakeItemEntityPacket>
    {
        //Vanilla order: writeVarInt itemId -> playerId -> amount, all three are VarInt
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
