namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMapItemDataPacket map item data packet, maps to vanilla ClientboundMapItemDataPacket
//Fields: MapId(MapId), Scale(byte), Locked(boolean), Decorations(Optional<List<MapDecoration>>), ColorPatch(Optional<MapItemSavedData.MapPatch>)
public sealed record ClientboundMapItemDataPacket(object MapId, byte Scale, bool Locked, object Decorations, object ColorPatch) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundMapItemDataPacket> StreamCodec { get; } = new MapItemDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMapItemData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleMapItemData(this);

    private sealed class MapItemDataCodec : StreamCodec<FriendlyByteBuf, ClientboundMapItemDataPacket>
    {
        public ClientboundMapItemDataPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundMapItemDataPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
