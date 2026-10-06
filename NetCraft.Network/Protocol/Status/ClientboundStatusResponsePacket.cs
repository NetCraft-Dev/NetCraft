namespace NetCraft.Network.Protocol.Status;

//ClientboundStatusResponsePacket client status response packet, maps to vanilla net.minecraft.network.protocol.status.ClientboundStatusResponsePacket
//The server returns a ServerStatus serialized as a JSON string
//The simplified form uses ToJson/FromJson instead of vanilla Codec + RegistryOps
public sealed record ClientboundStatusResponsePacket(ServerStatus Status) : Packet<ClientStatusPacketListener>
{
    //MaxStatusLength maximum status JSON length 32767
    public const int MaxStatusLength = 32767;

    //StreamCodec packet codec, maps to vanilla STREAM_CODEC
    public static StreamCodec<FriendlyByteBuf, ClientboundStatusResponsePacket> StreamCodec { get; } = new StatusResponseCodec();

    //Type packet type identity
    public PacketType<ClientStatusPacketListener> Type => StatusPacketTypes.ClientboundStatusResponse;

    //Handle calls the handler's HandleStatusResponse method
    public void Handle(ClientStatusPacketListener handler)
        => handler.HandleStatusResponse(this);

    //StatusResponseCodec codec reading and writing a JSON string
    private sealed class StatusResponseCodec : StreamCodec<FriendlyByteBuf, ClientboundStatusResponsePacket>
    {
        public ClientboundStatusResponsePacket Decode(FriendlyByteBuf buf)
        {
            var json = buf.ReadString(MaxStatusLength);
            var status = ServerStatus.FromJson(json) ?? new ServerStatus();
            return new ClientboundStatusResponsePacket(status);
        }

        public void Encode(FriendlyByteBuf buf, ClientboundStatusResponsePacket value)
            => buf.WriteString(value.Status.ToJson(), MaxStatusLength);
    }
}
