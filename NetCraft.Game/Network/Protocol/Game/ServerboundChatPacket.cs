namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatPacket 聊天包对应原版 ServerboundChatPacket
//原版字段 Message(string) TimeStamp(Instant) Salt(long) Signature(MessageSignature 可空) LastSeenMessages(LastSeenMessages.Update)
//简化: TimeStamp 存毫秒 long Signature 存原版固定的 256 字节
//LastSeenMessages.Update 展开为 offset + 20 位确认掩码 + 校验字节
public sealed record ServerboundChatPacket(
    string Message,
    long TimeStamp,
    long Salt,
    byte[]? Signature,
    int LastSeenOffset,
    int LastSeenAcknowledged,
    byte LastSeenChecksum) : Packet<ServerGamePacketListener>
{
    public const int MaxMessageLength = 256;
    //SignatureLength 原版 MessageSignature 固定 256 字节
    public const int SignatureLength = 256;
    //LastSeenBits 原版 writeFixedBitSet(acknowledged, 20) 的位数
    public const int LastSeenBits = 20;

    public static StreamCodec<FriendlyByteBuf, ServerboundChatPacket> StreamCodec { get; } = new ChatCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChat;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChat(this);

    //WriteFixedBitSet 原版 writeFixedBitSet 低位在前 固定 ceil(bits/8) 字节无长度前缀
    private static void WriteFixedBitSet(FriendlyByteBuf buf, int mask, int bits)
    {
        var bytes = new byte[(bits + 7) / 8];
        for (var i = 0; i < bits; i++)
            if ((mask & (1 << i)) != 0)
                bytes[i >> 3] |= (byte)(1 << (i & 7));
        buf.WriteBytes(bytes);
    }

    //ReadFixedBitSet 原版 readFixedBitSet 读入 ceil(bits/8) 字节还原位掩码
    private static int ReadFixedBitSet(FriendlyByteBuf buf, int bits)
    {
        var bytes = buf.ReadBytes((bits + 7) / 8);
        var mask = 0;
        for (var i = 0; i < bits; i++)
            if ((bytes[i >> 3] & (1 << (i & 7))) != 0)
                mask |= 1 << i;
        return mask;
    }

    private sealed class ChatCodec : StreamCodec<FriendlyByteBuf, ServerboundChatPacket>
    {
        //原版 write 顺序: writeUtf(message,256) writeInstant(timeStamp) writeLong(salt)
        //writeNullable(signature) 最后 LastSeenMessages.Update(VarInt offset + 3 字节 bitset + byte checksum)
        public ServerboundChatPacket Decode(FriendlyByteBuf buf)
        {
            var message = buf.ReadString(MaxMessageLength);
            var timeStamp = buf.ReadLong();
            var salt = buf.ReadLong();
            var signature = buf.ReadBoolean() ? buf.ReadBytes(SignatureLength) : null;
            var lastSeenOffset = buf.ReadVarInt();
            var acknowledged = ReadFixedBitSet(buf, LastSeenBits);
            var checksum = buf.ReadByte();
            return new(message, timeStamp, salt, signature, lastSeenOffset, acknowledged, checksum);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundChatPacket value)
        {
            buf.WriteString(value.Message, MaxMessageLength);
            buf.WriteLong(value.TimeStamp);
            buf.WriteLong(value.Salt);
            buf.WriteBoolean(value.Signature != null);
            if (value.Signature != null)
                buf.WriteBytes(value.Signature);
            buf.WriteVarInt(value.LastSeenOffset);
            WriteFixedBitSet(buf, value.LastSeenAcknowledged, LastSeenBits);
            buf.WriteByte(value.LastSeenChecksum);
        }
    }
}
