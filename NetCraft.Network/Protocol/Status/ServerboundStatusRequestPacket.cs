namespace NetCraft.Network.Protocol.Status;

//ServerboundStatusRequestPacket server status request packet, maps to vanilla net.minecraft.network.protocol.status.ServerboundStatusRequestPacket
//The client requests the server status; no payload, using the INSTANCE singleton
//StreamCodec.unit(INSTANCE) codes no data
public sealed record ServerboundStatusRequestPacket : Packet<ServerStatusPacketListener>
{
    //Instance singleton instance
    public static readonly ServerboundStatusRequestPacket Instance = new();

    //StreamCodec constant-value codec, maps to vanilla STREAM_CODEC = StreamCodec.unit(INSTANCE)
    public static StreamCodec<FriendlyByteBuf, ServerboundStatusRequestPacket> StreamCodec { get; }
        = new UnitStreamCodec<FriendlyByteBuf, ServerboundStatusRequestPacket>(Instance);

    private ServerboundStatusRequestPacket() { }

    //Type packet type identity
    public PacketType<ServerStatusPacketListener> Type => StatusPacketTypes.ServerboundStatusRequest;

    //Handle calls the handler's HandleStatusRequest method
    public void Handle(ServerStatusPacketListener handler)
        => handler.HandleStatusRequest(this);
}
