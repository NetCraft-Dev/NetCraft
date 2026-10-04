using NetCraft.Registry;

namespace NetCraft.Network.Protocol.Login;

//ClientboundCustomQueryPacket 服务端自定义查询包对应原版 net.minecraft.network.protocol.login.ClientboundCustomQueryPacket
//含 transactionId + payloadId Identifier + payload byte[]
//简化版用 byte[] 替代 CustomQueryPayload 跳过 custom 子协议
public sealed record ClientboundCustomQueryPacket(
    int TransactionId,
    Identifier PayloadId,
    byte[]? Data) : Packet<ClientLoginPacketListener>
{
    //MaxPayloadSize payload 最大 1MB
    public const int MaxPayloadSize = 1048576;

    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ClientboundCustomQueryPacket> StreamCodec { get; } = new CustomQueryCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundCustomQuery;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleCustomQuery(this);

    //CustomQueryCodec 编解码器读 transactionId + Identifier + 可选 byte[]
    private sealed class CustomQueryCodec : StreamCodec<FriendlyByteBuf, ClientboundCustomQueryPacket>
    {
        public ClientboundCustomQueryPacket Decode(FriendlyByteBuf buf)
        {
            int transactionId = buf.ReadVarInt();
            var payloadId = buf.ReadIdentifier();
            int length = buf.ReadableBytes;
            if (length < 0 || length > MaxPayloadSize)
                throw new InvalidOperationException($"payload 长度超限 {length}");
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
