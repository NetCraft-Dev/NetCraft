namespace NetCraft.Network.Protocol.Handshake;

//ClientIntentionPacket client handshake packet, maps to vanilla net.minecraft.network.protocol.handshake.ClientIntentionPacket
//Sent by the client when initiating the handshake, containing the protocol version, host, port, and intent
//Implements the Packet<ServerHandshakePacketListener> interface
//IsTerminal true means a protocol switch follows the handshake packet and it cannot stay in the HANDSHAKE state
public sealed record ClientIntentionPacket(
    int ProtocolVersion,
    string HostName,
    int Port,
    ClientIntent Intention) : Packet<ServerHandshakePacketListener>
{
    //MaxHostLength maximum host name length 255 bytes
    public const int MaxHostLength = 255;

    //StreamCodec packet codec, maps to vanilla STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientIntentionPacket> StreamCodec { get; } = new ClientIntentionCodec();

    //Type packet type identity
    public PacketType<ServerHandshakePacketListener> Type => HandshakePacketTypes.ClientIntention;

    //IsTerminal the handshake packet terminates the HANDSHAKE state, switching to STATUS or LOGIN
    public bool IsTerminal => true;

    //Handle calls the handler's handleIntention method
    public void Handle(ServerHandshakePacketListener handler)
        => handler.HandleIntention(this);

    //ClientIntentionCodec codec internal implementation
    //Reads VarInt protocol version + UTF-8 host name + ushort port + VarInt intent ID
    private sealed class ClientIntentionCodec : StreamCodec<FriendlyByteBuf, ClientIntentionPacket>
    {
        public ClientIntentionPacket Decode(FriendlyByteBuf buf)
        {
            int protocolVersion = buf.ReadVarInt();
            string hostName = buf.ReadString(MaxHostLength);
            int port = (ushort)buf.ReadShort();
            var intention = ClientIntentExtensions.ById(buf.ReadVarInt());
            return new ClientIntentionPacket(protocolVersion, hostName, port, intention);
        }

        public void Encode(FriendlyByteBuf buf, ClientIntentionPacket value)
        {
            buf.WriteVarInt(value.ProtocolVersion);
            buf.WriteString(value.HostName, MaxHostLength);
            buf.WriteShort((short)value.Port);
            buf.WriteVarInt(value.Intention.Id());
        }
    }
}
