using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEquipmentPacket 装备设置包对应原版 ClientboundSetEquipmentPacket
//字段 Entity(VarInt) Slots(List<KeyValuePair<EquipmentSlot, ItemStack>>)
//Slots 用 packed byte 编码低 7 bit 为 slot id 高位 0x80 为继续标记 最后一对不带标记
public sealed record ClientboundSetEquipmentPacket(int Entity, List<KeyValuePair<EquipmentSlot, ItemStack>> Slots) : Packet<ClientGamePacketListener>
{
    //ContinueMask 高位继续标记对应原版 CONTINUE_MASK
    private const byte ContinueMask = 0x80;

    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEquipmentPacket> StreamCodec { get; } = new SetEquipmentCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEquipment;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEquipment(this);

    private sealed class SetEquipmentCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEquipmentPacket>
    {
        public ClientboundSetEquipmentPacket Decode(RegistryFriendlyByteBuf buf)
        {
            int entityId = buf.ReadVarInt();
            var slots = new List<KeyValuePair<EquipmentSlot, ItemStack>>();
            byte packed;
            do
            {
                packed = buf.ReadByte();
                var slot = (EquipmentSlot)(packed & 0x7F);
                var stack = ItemStack.OptionalStreamCodec.Decode(buf);
                slots.Add(new(slot, stack));
            } while ((packed & ContinueMask) != 0);
            return new(entityId, slots);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundSetEquipmentPacket value)
        {
            buf.WriteVarInt(value.Entity);
            for (int i = 0; i < value.Slots.Count; i++)
            {
                var pair = value.Slots[i];
                bool isLast = i == value.Slots.Count - 1;
                buf.WriteByte((byte)((int)pair.Key | (isLast ? 0 : ContinueMask)));
                ItemStack.OptionalStreamCodec.Encode(buf, pair.Value);
            }
        }
    }
}
