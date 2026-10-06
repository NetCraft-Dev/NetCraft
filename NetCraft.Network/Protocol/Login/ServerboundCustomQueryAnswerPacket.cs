namespace NetCraft.Network.Protocol.Login;

//ServerboundCustomQueryAnswerPacket client custom query answer packet, maps to vanilla net.minecraft.network.protocol.login.ServerboundCustomQueryAnswerPacket
//The simplified form uses byte[] instead of CustomQueryAnswerPayload and skips the custom subprotocol
//Vanilla reads transactionId + payload; the simplified form reads transactionId + byte length
public sealed record ServerboundCustomQueryAnswerPacket(int TransactionId, byte[]? Data) : Packet<ServerLoginPacketListener>
{
    //MaxPayloadSize maximum payload 1MB
    public const int MaxPayloadSize = 1048576;

    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ServerboundCustomQueryAnswerPacket> StreamCodec { get; } = new CustomQueryAnswerCodec();

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundCustomQueryAnswer;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleCustomQueryPacket(this);

    //CustomQueryAnswerCodec codec reading transactionId + optional byte[]
    private sealed class CustomQueryAnswerCodec : StreamCodec<FriendlyByteBuf, ServerboundCustomQueryAnswerPacket>
    {
        public ServerboundCustomQueryAnswerPacket Decode(FriendlyByteBuf buf)
        {
            int transactionId = buf.ReadVarInt();
            //The simplified form reads the remaining bytes directly as payload
            int length = buf.ReadableBytes;
            if (length < 0 || length > MaxPayloadSize)
                throw new InvalidOperationException($"payload length out of range {length}");
            byte[]? data = length > 0 ? buf.ReadBytes(length) : null;
            return new ServerboundCustomQueryAnswerPacket(transactionId, data);
        }

        public void Encode(FriendlyByteBuf buf, ServerboundCustomQueryAnswerPacket value)
        {
            buf.WriteVarInt(value.TransactionId);
            buf.WriteNullable(value.Data, (b, v) => b.WriteByteArray(v));
        }
    }
}
