namespace NetCraft.Network.Protocol.Login;

//ClientboundLoginCompressionPacket 服务端压缩通知包对应原版 net.minecraft.network.protocol.login.ClientboundLoginCompressionPacket
//通知客户端后续包启用压缩 threshold 为压缩阈值
public sealed record ClientboundLoginCompressionPacket(int CompressionThreshold) : Packet<ClientLoginPacketListener>
{
    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginCompressionPacket> StreamCodec { get; } = new CompressionCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundLoginCompression;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleCompression(this);

    //CompressionCodec 编解码器读写 VarInt compressionThreshold
    private sealed class CompressionCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginCompressionPacket>
    {
        public ClientboundLoginCompressionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundLoginCompressionPacket value)
            => buf.WriteVarInt(value.CompressionThreshold);
    }
}
