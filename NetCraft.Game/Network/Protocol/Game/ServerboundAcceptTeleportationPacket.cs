namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundAcceptTeleportationPacket client accept teleportation packet, maps to vanilla ServerboundAcceptTeleportationPacket
//Field: Id(int)
public sealed record ServerboundAcceptTeleportationPacket(int Id) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundAcceptTeleportationPacket> StreamCodec { get; } = new AcceptTeleportationCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundAcceptTeleportation;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAcceptTeleportPacket(this);

    private sealed class AcceptTeleportationCodec : StreamCodec<FriendlyByteBuf, ServerboundAcceptTeleportationPacket>
    {
        //Vanilla writeVarInt(id), the teleport id sent by the client
        public ServerboundAcceptTeleportationPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundAcceptTeleportationPacket value)
            => buf.WriteVarInt(value.Id);
    }
}
