namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatAckPacket chat message acknowledgment, maps to vanilla ServerboundChatAckPacket
//After the 26.2 signing system was slimmed down, only Offset(VarInt) remains, the message position the client has read
public sealed record ServerboundChatAckPacket(int Offset) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatAckPacket> StreamCodec { get; } = new ChatAckCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatAck;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatAck(this);

    private sealed class ChatAckCodec : StreamCodec<FriendlyByteBuf, ServerboundChatAckPacket>
    {
        public ServerboundChatAckPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundChatAckPacket value)
            => buf.WriteVarInt(value.Offset);
    }
}
