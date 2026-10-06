namespace NetCraft.Network.Protocol.Login;

//ClientboundLoginDisconnectPacket server login disconnect packet, maps to vanilla net.minecraft.network.protocol.login.ClientboundLoginDisconnectPacket
//Contains reason, the disconnect reason; vanilla uses a Component, the simplified form uses string
//IsTerminal true means the connection closes after disconnecting
public sealed record ClientboundLoginDisconnectPacket(string Reason) : Packet<ClientLoginPacketListener>
{
    //MaxReasonLength maximum reason string length, aligns with FriendlyByteBuf.MAX_COMPONENT_STRING_LENGTH
    public const int MaxReasonLength = 262144;

    //StreamCodec packet codec
    public static StreamCodec<FriendlyByteBuf, ClientboundLoginDisconnectPacket> StreamCodec { get; } = new DisconnectCodec();

    public PacketType<ClientLoginPacketListener> Type => LoginPacketTypes.ClientboundLoginDisconnect;

    //IsTerminal the connection closes after the disconnect packet
    public bool IsTerminal => true;

    public void Handle(ClientLoginPacketListener handler) => handler.HandleDisconnect(this);

    //DisconnectCodec codec reading and writing the reason string
    private sealed class DisconnectCodec : StreamCodec<FriendlyByteBuf, ClientboundLoginDisconnectPacket>
    {
        public ClientboundLoginDisconnectPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadString(MaxReasonLength));

        public void Encode(FriendlyByteBuf buf, ClientboundLoginDisconnectPacket value)
            => buf.WriteString(value.Reason, MaxReasonLength);
    }
}
