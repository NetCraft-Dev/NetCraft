namespace NetCraft.Network.Protocol.Login;

//ServerboundLoginAcknowledgedPacket client login acknowledged packet, maps to vanilla net.minecraft.network.protocol.login.ServerboundLoginAcknowledgedPacket
//No payload, singleton pattern; IsTerminal true means the LOGIN phase ends and it switches to CONFIGURATION
public sealed record ServerboundLoginAcknowledgedPacket : Packet<ServerLoginPacketListener>
{
    //Instance singleton instance
    public static readonly ServerboundLoginAcknowledgedPacket Instance = new();

    //StreamCodec constant-value codec
    public static StreamCodec<FriendlyByteBuf, ServerboundLoginAcknowledgedPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundLoginAcknowledgedPacket>(Instance);

    private ServerboundLoginAcknowledgedPacket() { }

    public PacketType<ServerLoginPacketListener> Type => LoginPacketTypes.ServerboundLoginAcknowledged;

    //IsTerminal switches to CONFIGURATION after login acknowledgement
    public bool IsTerminal => true;

    public void Handle(ServerLoginPacketListener handler) => handler.HandleLoginAcknowledgement(this);
}
