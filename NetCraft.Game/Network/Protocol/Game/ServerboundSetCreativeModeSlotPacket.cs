using NetCraft.Game.World.Items;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCreativeModeSlotPacket 数据包对应原版 ServerboundSetCreativeModeSlotPacket
//字段 SlotNum(short) Stack(ItemStack 可空) 创造背包槽位设置与丢弃均走本包
//字段名 Stack 避免与 ItemStack 类型名冲突
public sealed record ServerboundSetCreativeModeSlotPacket(short SlotNum, ItemStack Stack) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ServerboundSetCreativeModeSlotPacket> StreamCodec { get; } = new SetCreativeModeSlotCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCreativeModeSlot;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCreativeModeSlot(this);

    private sealed class SetCreativeModeSlotCodec : StreamCodec<RegistryFriendlyByteBuf, ServerboundSetCreativeModeSlotPacket>
    {
        //物品栈用可空编解码对应原版 OPTIONAL_UNTRUSTED_STREAM_CODEC
        public ServerboundSetCreativeModeSlotPacket Decode(RegistryFriendlyByteBuf buf)
        {
            var slotNum = buf.ReadShort();
            var stack = ItemStack.OptionalStreamCodec.Decode(buf);
            return new(slotNum, stack);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ServerboundSetCreativeModeSlotPacket value)
        {
            buf.WriteShort(value.SlotNum);
            ItemStack.OptionalStreamCodec.Encode(buf, value.Stack);
        }
    }
}
