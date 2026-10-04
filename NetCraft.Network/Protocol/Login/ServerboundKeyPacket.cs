namespace NetCraft.Network.Protocol.Login;

//ServerboundKeyPacket 客户端加密密钥包对应原版 net.minecraft.network.protocol.login.ServerboundKeyPacket
//含加密后的 secretKey 字节和加密后的 challenge 字节
//简化版不实现 RSA 加密逻辑只保留 byte[] 字段传输
//原版用 Crypt.encryptUsingKey RSA 加密简化版由调用方提供加密后字节
public sealed record ServerboundKeyPacket(byte[] KeyBytes, byte[] EncryptedChallenge) : Packet<ServerLoginPacketListener>
{
    //StreamCodec 包编解码器
    public static StreamCodec<FriendlyByteBuf, ServerboundKeyPacket> StreamCodec { get; } = new KeyCodec();

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundKey;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleKey(this);

    //KeyCodec 编解码器读写两个 byte[]
    private sealed class KeyCodec : StreamCodec<FriendlyByteBuf, ServerboundKeyPacket>
    {
        public ServerboundKeyPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadByteArray(), buf.ReadByteArray());

        public void Encode(FriendlyByteBuf buf, ServerboundKeyPacket value)
        {
            buf.WriteByteArray(value.KeyBytes);
            buf.WriteByteArray(value.EncryptedChallenge);
        }
    }
}
