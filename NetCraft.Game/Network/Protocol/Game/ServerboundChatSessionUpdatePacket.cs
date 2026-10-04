namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatSessionUpdatePacket 聊天签名会话密钥更新对应原版 ServerboundChatSessionUpdatePacket
//字段为 RemoteChatSession.Data sessionId(UUID) 过期时间(Instant) 公钥与签名
//服务端不验签 字段只做保真解析
public sealed record ServerboundChatSessionUpdatePacket(
    Guid SessionId, long ExpiresAt, byte[] PublicKey, byte[] KeySignature) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatSessionUpdatePacket> StreamCodec { get; } = new ChatSessionUpdateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatSessionUpdate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatSessionUpdate(this);

    private sealed class ChatSessionUpdateCodec : StreamCodec<FriendlyByteBuf, ServerboundChatSessionUpdatePacket>
    {
        //原版顺序 readUUID readInstant readPublicKey(readByteArray 512) readByteArray(4096)
        public ServerboundChatSessionUpdatePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadUuid(), buf.ReadLong(), buf.ReadByteArray(512), buf.ReadByteArray(4096));

        public void Encode(FriendlyByteBuf buf, ServerboundChatSessionUpdatePacket value)
        {
            buf.WriteUuid(value.SessionId);
            buf.WriteLong(value.ExpiresAt);
            buf.WriteByteArray(value.PublicKey, 512);
            buf.WriteByteArray(value.KeySignature, 4096);
        }
    }
}
