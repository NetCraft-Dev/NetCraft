namespace NetCraft.Network.Protocol.Login;

//ClientboundLoginCompressionPacket server compression notification packet, maps to vanilla net.minecraft.network.protocol.login.ClientboundLoginCompressionPacket
//Notifies the client that subsequent packets enable compression; threshold is the compression threshold
public sealed record ClientboundLoginCompressionPacket(int CompressionThreshold) : Packet<ClientLoginPacketListener>
{
    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginCompressionPacket> StreamCodec { get; } = new CompressionCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundLoginCompression;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleCompression(this);

    //CompressionCodec codec reading and writing VarInt compressionThreshold
    private sealed class CompressionCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginCompressionPacket>
    {
        public ClientboundLoginCompressionPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundLoginCompressionPacket value)
            => buf.WriteVarInt(value.CompressionThreshold);
    }
}
