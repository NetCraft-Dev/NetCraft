namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSelectTradePacket select trade packet, maps to vanilla ServerboundSelectTradePacket
//Field: Item(int)
public sealed record ServerboundSelectTradePacket(int Item) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSelectTradePacket> StreamCodec { get; } = new SelectTradeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSelectTrade;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSelectTrade(this);

    private sealed class SelectTradeCodec : StreamCodec<FriendlyByteBuf, ServerboundSelectTradePacket>
    {
        //Sent when selecting a trade in the villager trading screen; carries only the trade index
        public ServerboundSelectTradePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundSelectTradePacket value)
            => buf.WriteVarInt(value.Item);
    }
}
