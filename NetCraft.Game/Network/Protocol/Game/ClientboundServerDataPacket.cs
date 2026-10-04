namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundServerDataPacket 服务器数据包对应原版 ClientboundServerDataPacket
//字段 Motd(Component)
public sealed record ClientboundServerDataPacket(Component Motd) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundServerDataPacket> StreamCodec { get; } = new ServerDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundServerData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleServerData(this);

    private sealed class ServerDataCodec : StreamCodec<FriendlyByteBuf, ClientboundServerDataPacket>
    {
        public ClientboundServerDataPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundServerDataPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
