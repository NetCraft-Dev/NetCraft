namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundClientTickEndPacket client tick end packet, maps to vanilla ServerboundClientTickEndPacket
//Fields:
public sealed record ServerboundClientTickEndPacket() : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundClientTickEndPacket> StreamCodec { get; } = new ClientTickEndCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundClientTickEnd;

    public void Handle(ServerGamePacketListener handler) => handler.HandleClientTickEnd(this);

    private sealed class ClientTickEndCodec : StreamCodec<FriendlyByteBuf, ServerboundClientTickEndPacket>
    {
        public ServerboundClientTickEndPacket Decode(FriendlyByteBuf buf)
            => new();

        public void Encode(FriendlyByteBuf buf, ServerboundClientTickEndPacket value)
            { }
    }
}
