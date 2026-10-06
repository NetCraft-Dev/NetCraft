using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Login;

//ClientboundCustomQueryPacket server custom query packet, maps to vanilla net.minecraft.network.protocol.login.ClientboundCustomQueryPacket
//Contains transactionId + payloadId Identifier + payload byte[]
//The simplified form uses byte[] instead of CustomQueryPayload and skips the custom subprotocol
public sealed record ClientboundCustomQueryPacket(
    int TransactionId,
    Identifier PayloadId,
    byte[]? Data) : Packet<ClientLoginPacketListener>
{
    //MaxPayloadSize maximum payload 1MB
    public const int MaxPayloadSize = 1048576;

    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomQueryPacket> StreamCodec { get; } = new CustomQueryCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundCustomQuery;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleCustomQuery(this);

    //CustomQueryCodec codec reading transactionId + Identifier + optional byte[]
    private sealed class CustomQueryCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomQueryPacket>
    {
        public ClientboundCustomQueryPacket Decode(FriendlyByteBuf buf)
        {
            int transactionId = buf.ReadVarInt();
            var payloadId = buf.ReadIdentifier();
            int length = buf.ReadableBytes;
            if (length < 0 || length > MaxPayloadSize)
                throw new InvalidOperationException($"payload length out of range {length}");
            byte[]? data = length > 0 ? buf.ReadBytes(length) : null;
            return new ClientboundCustomQueryPacket(transactionId, payloadId, data);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundCustomQueryPacket value)
        {
            buf.WriteVarInt(value.TransactionId);
            buf.WriteIdentifier(value.PayloadId);
            buf.WriteNullable(value.Data, (b, v) => b.WriteByteArray(v));
        }
    }
}
