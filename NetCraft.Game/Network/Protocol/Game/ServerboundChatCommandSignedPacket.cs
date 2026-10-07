namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundChatCommandSignedPacket signed slash command, maps to vanilla ServerboundChatCommandSignedPacket
//Fields: Command(String), TimeStamp(long), Salt(long), ArgumentSignatures, LastSeenMessages.Update
//The server does not verify signatures; each field is only parsed faithfully
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
        //Vanilla order: readUtf, readInstant, readLong, ArgumentSignatures (VarInt count + [utf + 256 bytes]), Update (offset + 3-byte bitset + checksum)
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

    //WriteFixedBitSet LSB first, a fixed ceil(bits/8) bytes with no length prefix
    private static void WriteFixedBitSet(FriendlyByteBuf buf, int mask, int bits)
    {
        var bytes = new byte[(bits + 7) / 8];
        for (var i = 0; i < bits; i++)
            if ((mask & (1 << i)) != 0)
                bytes[i >> 3] |= (byte)(1 << (i & 7));
        buf.WriteBytes(bytes);
    }

    //ReadFixedBitSet reads ceil(bits/8) bytes and restores the bitmask
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

//ArgumentSignatureEntry command argument signature entry, maps to vanilla ArgumentSignatures.Entry
public readonly record struct ArgumentSignatureEntry(string Name, byte[] Signature);
