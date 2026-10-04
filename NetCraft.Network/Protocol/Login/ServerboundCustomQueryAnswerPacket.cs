namespace NetCraft.Network.Protocol.Login;

//ServerboundCustomQueryAnswerPacket 客户端自定义查询应答包对应原版 net.minecraft.network.protocol.login.ServerboundCustomQueryAnswerPacket
//简化版用 byte[] 替代 CustomQueryAnswerPayload 跳过 custom 子协议
//原版读 transactionId + payload 简化版读 transactionId + 字节长度
public sealed record ServerboundCustomQueryAnswerPacket(int TransactionId, byte[]? Data) : Packet<ServerLoginPacketListener>
{
    //MaxPayloadSize payload 最大 1MB
    public const int MaxPayloadSize = 1048576;

    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ServerboundCustomQueryAnswerPacket> StreamCodec { get; } = new CustomQueryAnswerCodec();

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundCustomQueryAnswer;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleCustomQueryPacket(this);

    //CustomQueryAnswerCodec 编解码器读 transactionId + 可选 byte[]
    private sealed class CustomQueryAnswerCodec : StreamCodec<FriendlyByteBuf, ServerboundCustomQueryAnswerPacket>
    {
        public ServerboundCustomQueryAnswerPacket Decode(FriendlyByteBuf buf)
        {
            int transactionId = buf.ReadVarInt();
            //简化版直接读剩余字节作为 payload
            int length = buf.ReadableBytes;
            if (length < 0 || length > MaxPayloadSize)
                throw new InvalidOperationException($"payload 长度超限 {length}");
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
