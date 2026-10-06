namespace NetCraft.Network.Protocol.Login;

//ServerboundKeyPacket client encryption key packet, maps to vanilla net.minecraft.network.protocol.login.ServerboundKeyPacket
//Contains the encrypted secretKey bytes and the encrypted challenge bytes
//The simplified form does not implement RSA encryption and only keeps the byte[] fields for transport
//Vanilla encrypts with Crypt.encryptUsingKey RSA; in the simplified form the caller provides the encrypted bytes
public sealed record ServerboundKeyPacket(byte[] KeyBytes, byte[] EncryptedChallenge) : Packet<ServerLoginPacketListener>
{
    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ServerboundKeyPacket> StreamCodec { get; } = new KeyCodec();

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundKey;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleKey(this);

    //KeyCodec codec reading and writing two byte[]
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
