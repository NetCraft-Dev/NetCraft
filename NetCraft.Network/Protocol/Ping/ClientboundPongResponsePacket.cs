namespace NetCraft.Network.Protocol.Ping;

//ClientboundPongResponsePacket client pong response packet, maps to vanilla net.minecraft.network.protocol.ping.ClientboundPongResponsePacket
//The server returns the time timestamp of the ping request and the client computes the round-trip latency
//Handler uses ClientPongPacketListener, aligning with vanilla Packet<? super T> contravariance; it is registered in both the Status and Play protocols
public sealed record ClientboundPongResponsePacket(long Time) : Packet<ClientPongPacketListener>
{
    //StreamCodec packet codec, maps to vanilla STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundPongResponsePacket> StreamCodec { get; } = new PongResponseCodec();

    //Type packet type identity, used only as an identity and no longer determines the network ID
    //The same packet class has different IDs in Status and Play; on encode the current protocol table looks it up by packet class
    public PacketType<ClientPongPacketListener> Type => PingPacketTypes.ClientboundPongResponse;

    //Handle calls the handler's HandlePongResponse method
    public void Handle(ClientPongPacketListener handler)
        => handler.HandlePongResponse(this);

    //PongResponseCodec codec reading and writing long time
    private sealed class PongResponseCodec : StreamCodec<FriendlyByteBuf, ClientboundPongResponsePacket>
    {
        public ClientboundPongResponsePacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());

        public void Encode(FriendlyByteBuf buf, ClientboundPongResponsePacket value)
            => buf.WriteLong(value.Time);
    }
}
