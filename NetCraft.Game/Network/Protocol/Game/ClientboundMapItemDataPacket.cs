namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMapItemDataPacket 地图数据包对应原版 ClientboundMapItemDataPacket
//字段 MapId(MapId) Scale(byte) Locked(boolean) Decorations(Optional<List<MapDecoration>>) ColorPatch(Optional<MapItemSavedData.MapPatch>)
public sealed record ClientboundMapItemDataPacket(object MapId, byte Scale, bool Locked, object Decorations, object ColorPatch) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMapItemDataPacket> StreamCodec { get; } = new MapItemDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMapItemData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMapItemData(this);

    private sealed class MapItemDataCodec : StreamCodec<FriendlyByteBuf, ClientboundMapItemDataPacket>
    {
        public ClientboundMapItemDataPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundMapItemDataPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
