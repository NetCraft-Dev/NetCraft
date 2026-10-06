namespace NetCraft.Network.Protocol.Ping;

//ServerboundPingRequestPacket server ping request packet, maps to vanilla net.minecraft.network.protocol.ping.ServerboundPingRequestPacket
//Sent by the client with a time timestamp, used for ping measurement
//Handler uses ServerPingPacketListener, aligning with vanilla Packet<? super T> contravariance; it is registered in both the Status and Play protocols
public sealed record ServerboundPingRequestPacket(long Time) : Packet<ServerPingPacketListener>
{
    //StreamCodec packet codec, maps to vanilla STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ServerboundPingRequestPacket> StreamCodec { get; } = new PingRequestCodec();

    //Type packet type identity
    public PacketType<ServerPingPacketListener> Type => PingPacketTypes.ServerboundPingRequest;

    //Handle calls the handler's HandlePingRequest method
    public void Handle(ServerPingPacketListener handler)
        => handler.HandlePingRequest(this);

    //PingRequestCodec codec reading and writing long time
    private sealed class PingRequestCodec : StreamCodec<FriendlyByteBuf, ServerboundPingRequestPacket>
    {
        public ServerboundPingRequestPacket Decode(FriendlyByteBuf buf) => new(buf.ReadLong());

        public void Encode(FriendlyByteBuf buf, ServerboundPingRequestPacket value)
            => buf.WriteLong(value.Time);
    }
}
