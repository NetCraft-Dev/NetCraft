namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundServerDataPacket server data packet, maps to vanilla ClientboundServerDataPacket
//Field: Motd(Component)
public sealed record ClientboundServerDataPacket(Component Motd) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundServerDataPacket> StreamCodec { get; } = new ServerDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundServerData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleServerData(this);

    private sealed class ServerDataCodec : StreamCodec<FriendlyByteBuf, ClientboundServerDataPacket>
    {
        public ClientboundServerDataPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundServerDataPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
