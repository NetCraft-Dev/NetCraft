namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatSessionUpdatePacket chat signing session key update, maps to vanilla ServerboundChatSessionUpdatePacket
//Fields are RemoteChatSession.Data: sessionId(UUID), expiry (Instant), public key and signature
//The server does not verify signatures; fields are only parsed faithfully
public sealed record ServerboundChatSessionUpdatePacket(
    Guid SessionId, long ExpiresAt, byte[] PublicKey, byte[] KeySignature) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundChatSessionUpdatePacket> StreamCodec { get; } = new ChatSessionUpdateCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatSessionUpdate;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChatSessionUpdate(this);

    private sealed class ChatSessionUpdateCodec : StreamCodec<FriendlyByteBuf, ServerboundChatSessionUpdatePacket>
    {
        //Vanilla order: readUUID, readInstant, readPublicKey(readByteArray 512), readByteArray(4096)
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
