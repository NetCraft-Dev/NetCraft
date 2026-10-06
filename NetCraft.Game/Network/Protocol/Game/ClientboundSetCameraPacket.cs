namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetCameraPacket set camera packet, maps to vanilla ClientboundSetCameraPacket
//Field: CameraId(int), the target entity network id; the client switches its camera to that entity
public sealed record ClientboundSetCameraPacket(int CameraId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundSetCameraPacket> StreamCodec { get; } = new SetCameraCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetCamera;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetCamera(this);

    private sealed class SetCameraCodec : StreamCodec<FriendlyByteBuf, ClientboundSetCameraPacket>
    {
        //Vanilla write writes only a single VarInt entity id
        public ClientboundSetCameraPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ClientboundSetCameraPacket value)
            => buf.WriteVarInt(value.CameraId);
    }
}
