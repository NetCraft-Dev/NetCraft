namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatPacket chat packet, maps to vanilla ServerboundChatPacket
//Vanilla fields: Message(string), TimeStamp(Instant), Salt(long), Signature(MessageSignature, nullable), LastSeenMessages(LastSeenMessages.Update)
//Simplified: TimeStamp is stored as a millisecond long and Signature as the vanilla fixed 256 bytes
//LastSeenMessages.Update is expanded into offset + 20-bit acknowledgment mask + checksum byte
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
    //SignatureLength the vanilla MessageSignature fixed 256 bytes
    public const int SignatureLength = 256;
    //LastSeenBits the bit count of vanilla writeFixedBitSet(acknowledged, 20)
    public const int LastSeenBits = 20;

    public static StreamCodec<FriendlyByteBuf, ServerboundChatPacket> StreamCodec { get; } = new ChatCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChat;

    public void Handle(ServerGamePacketListener handler) => handler.HandleChat(this);

    //WriteFixedBitSet vanilla writeFixedBitSet LSB first, a fixed ceil(bits/8) bytes with no length prefix
    private static void WriteFixedBitSet(FriendlyByteBuf buf, int mask, int bits)
    {
        var bytes = new byte[(bits + 7) / 8];
        for (var i = 0; i < bits; i++)
            if ((mask & (1 << i)) != 0)
                bytes[i >> 3] |= (byte)(1 << (i & 7));
        buf.WriteBytes(bytes);
    }

    //ReadFixedBitSet vanilla readFixedBitSet reads ceil(bits/8) bytes and restores the bitmask
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
        //Vanilla write order: writeUtf(message,256), writeInstant(timeStamp), writeLong(salt)
        //writeNullable(signature), then LastSeenMessages.Update(VarInt offset + 3-byte bitset + byte checksum)
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
