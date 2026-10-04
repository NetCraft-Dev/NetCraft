namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundClientTickEndPacket 数据包对应原版 ServerboundClientTickEndPacket
//字段 
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
