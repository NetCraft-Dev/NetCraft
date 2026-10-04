namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatAckPacket 聊天消息确认对应原版 ServerboundChatAckPacket
//26.2 签名系统精简后只剩 Offset(VarInt) 表示客户端已读到的消息位
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
