namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMountScreenOpenPacket 骑乘界面打开包对应原版 ClientboundMountScreenOpenPacket
//字段 ContainerId(int) InventoryColumns(int) EntityId(int)
public sealed record ClientboundMountScreenOpenPacket(int ContainerId, int InventoryColumns, int EntityId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMountScreenOpenPacket> StreamCodec { get; } = new MountScreenOpenCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMountScreenOpen;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMountScreenOpen(this);

    private sealed class MountScreenOpenCodec : StreamCodec<FriendlyByteBuf, ClientboundMountScreenOpenPacket>
    {
        public ClientboundMountScreenOpenPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundMountScreenOpenPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
