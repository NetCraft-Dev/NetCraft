namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundAcceptTeleportationPacket 客户端确认传送包对应原版 ServerboundAcceptTeleportationPacket
//字段 Id(int)
public sealed record ServerboundAcceptTeleportationPacket(int Id) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundAcceptTeleportationPacket> StreamCodec { get; } = new AcceptTeleportationCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundAcceptTeleportation;

    public void Handle(ServerGamePacketListener handler) => handler.HandleAcceptTeleportPacket(this);

    private sealed class AcceptTeleportationCodec : StreamCodec<FriendlyByteBuf, ServerboundAcceptTeleportationPacket>
    {
        //原版 writeVarInt(id) 对应客户端发来的传送 id
        public ServerboundAcceptTeleportationPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundAcceptTeleportationPacket value)
            => buf.WriteVarInt(value.Id);
    }
}
