namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMountScreenOpenPacket mount screen open packet, maps to vanilla ClientboundMountScreenOpenPacket
//Fields: ContainerId(int), InventoryColumns(int), EntityId(int)
public sealed record ClientboundMountScreenOpenPacket(int ContainerId, int InventoryColumns, int EntityId) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMountScreenOpenPacket> StreamCodec { get; } = new MountScreenOpenCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMountScreenOpen;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMountScreenOpen(this);

    private sealed class MountScreenOpenCodec : StreamCodec<FriendlyByteBuf, ClientboundMountScreenOpenPacket>
    {
        public ClientboundMountScreenOpenPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundMountScreenOpenPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
