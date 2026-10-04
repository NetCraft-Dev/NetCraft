namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSelectBundleItemPacket 数据包对应原版 ServerboundSelectBundleItemPacket
//字段 SlotId(int) SelectedItemIndex(int)
public sealed record ServerboundSelectBundleItemPacket(int SlotId, int SelectedItemIndex) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundSelectBundleItemPacket> StreamCodec { get; } = new SelectBundleItemCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundBundleItemSelected;

    public void Handle(ServerGamePacketListener handler) => handler.HandleBundleItemSelectedPacket(this);

    private sealed class SelectBundleItemCodec : StreamCodec<FriendlyByteBuf, ServerboundSelectBundleItemPacket>
    {
        public ServerboundSelectBundleItemPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ServerboundSelectBundleItemPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
