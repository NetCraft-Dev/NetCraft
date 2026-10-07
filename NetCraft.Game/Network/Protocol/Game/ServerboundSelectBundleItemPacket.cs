namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSelectBundleItemPacket select bundle item packet, maps to vanilla ServerboundSelectBundleItemPacket
//Fields: SlotId(int), SelectedItemIndex(int)
public sealed record ServerboundSelectBundleItemPacket(int SlotId, int SelectedItemIndex) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSelectBundleItemPacket> StreamCodec { get; } = new SelectBundleItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundBundleItemSelected;

    public void Handle(ServerGamePacketListener handler) => handler.HandleBundleItemSelectedPacket(this);

    private sealed class SelectBundleItemCodec : StreamCodec<FriendlyByteBuf, ServerboundSelectBundleItemPacket>
    {
        public ServerboundSelectBundleItemPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ServerboundSelectBundleItemPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
