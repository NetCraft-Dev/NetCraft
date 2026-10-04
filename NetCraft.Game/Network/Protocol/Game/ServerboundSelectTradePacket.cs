namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSelectTradePacket 数据包对应原版 ServerboundSelectTradePacket
//字段 Item(int)
public sealed record ServerboundSelectTradePacket(int Item) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSelectTradePacket> StreamCodec { get; } = new SelectTradeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSelectTrade;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSelectTrade(this);

    private sealed class SelectTradeCodec : StreamCodec<FriendlyByteBuf, ServerboundSelectTradePacket>
    {
        //村民交易界面点选商品时发送 只有商品序号
        public ServerboundSelectTradePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundSelectTradePacket value)
            => buf.WriteVarInt(value.Item);
    }
}
