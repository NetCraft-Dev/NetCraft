namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatCommandSignedPacket 带签名的斜杠命令对应原版 ServerboundChatCommandSignedPacket
//字段 Command(String) TimeStamp(long) Salt(long) ArgumentSignatures LastSeenMessages.Update
//服务端不验签 各字段只做保真解析
public sealed record ServerboundChatCommandSignedPacket(
    string Command, long TimeStamp, long Salt,
    IReadOnlyList<ArgumentSignatureEntry> ArgumentSignatures,
    int LastSeenOffset, int LastSeenAcknowledged, byte LastSeenChecksum) : Packet<ServerGamePacketListener>
{
    public const int LastSeenBits = 20;

    public static StreamCodec<FriendlyByteBuf, ServerboundChatCommandSignedPacket> StreamCodec { get; } = new ChatCommandSignedCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundChatCommandSigned;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSignedChatCommand(this);

    private sealed class ChatCommandSignedCodec : StreamCodec<FriendlyByteBuf, ServerboundChatCommandSignedPacket>
    {
        //原版顺序 readUtf readInstant readLong ArgumentSignatures(VarInt count + [utf + 256字节]) Update(offset + 3字节bitset + checksum)
        public ServerboundChatCommandSignedPacket Decode(FriendlyByteBuf buf)
        {
            var command = buf.ReadString();
            var timeStamp = buf.ReadLong();
            var salt = buf.ReadLong();
            var count = buf.ReadVarInt();
            var signatures = new List<ArgumentSignatureEntry>(Math.Clamp(count, 0, 8));
            for (var i = 0; i < count; i++)
                signatures.Add(new ArgumentSignatureEntry(buf.ReadString(), buf.ReadBytes(256)));
            var offset = buf.ReadVarInt();
            var acknowledged = ReadFixedBitSet(buf, LastSeenBits);
            var checksum = buf.ReadByte();
            return new(command, timeStamp, salt, signatures, offset, acknowledged, checksum);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundChatCommandSignedPacket value)
        {
            buf.WriteString(value.Command);
            buf.WriteLong(value.TimeStamp);
            buf.WriteLong(value.Salt);
            buf.WriteVarInt(value.ArgumentSignatures.Count);
            foreach (var entry in value.ArgumentSignatures)
            {
                buf.WriteString(entry.Name);
                buf.WriteBytes(entry.Signature);
            }
            buf.WriteVarInt(value.LastSeenOffset);
            WriteFixedBitSet(buf, value.LastSeenAcknowledged, LastSeenBits);
            buf.WriteByte(value.LastSeenChecksum);
        }
    }

    //WriteFixedBitSet 低位在前 固定 ceil(bits/8) 字节无长度前缀
    private static void WriteFixedBitSet(FriendlyByteBuf buf, int mask, int bits)
    {
        var bytes = new byte[(bits + 7) / 8];
        for (var i = 0; i < bits; i++)
            if ((mask & (1 << i)) != 0)
                bytes[i >> 3] |= (byte)(1 << (i & 7));
        buf.WriteBytes(bytes);
    }

    //ReadFixedBitSet 读 ceil(bits/8) 字节还原位掩码
    private static int ReadFixedBitSet(FriendlyByteBuf buf, int bits)
    {
        var bytes = buf.ReadBytes((bits + 7) / 8);
        var mask = 0;
        for (var i = 0; i < bits; i++)
            if ((bytes[i >> 3] & (1 << (i & 7))) != 0)
                mask |= 1 << i;
        return mask;
    }
}

//ArgumentSignatureEntry 命令参数签名条目对应原版 ArgumentSignatures.Entry
public readonly record struct ArgumentSignatureEntry(string Name, byte[] Signature);
