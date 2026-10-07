using NetCraft.Game.World.Items;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundSetCreativeModeSlotPacket set creative mode slot packet, maps to vanilla ServerboundSetCreativeModeSlotPacket
//Fields: SlotNum(short), Stack(ItemStack, nullable); both setting a creative inventory slot and dropping go through this packet
//The field is named Stack to avoid clashing with the ItemStack type name
public sealed record ServerboundSetCreativeModeSlotPacket(short SlotNum, ItemStack Stack) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ServerboundSetCreativeModeSlotPacket> StreamCodec { get; } = new SetCreativeModeSlotCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundSetCreativeModeSlot;

    public void Handle(ServerGamePacketListener handler) => handler.HandleSetCreativeModeSlot(this);

    private sealed class SetCreativeModeSlotCodec : StreamCodec<RegistryFriendlyByteBuf, ServerboundSetCreativeModeSlotPacket>
    {
        //The item stack uses the nullable codec, maps to vanilla OPTIONAL_UNTRUSTED_STREAM_CODEC
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
