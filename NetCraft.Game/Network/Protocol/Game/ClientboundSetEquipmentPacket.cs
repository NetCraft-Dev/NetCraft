using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetEquipmentPacket set equipment packet, maps to vanilla ClientboundSetEquipmentPacket
//Fields: Entity(VarInt), Slots(List<KeyValuePair<EquipmentSlot, ItemStack>>)
//Slots uses a packed byte encoding: the low 7 bits are the slot id, the high 0x80 bit is the continue marker, and the last pair carries no marker
public sealed record ClientboundSetEquipmentPacket(int Entity, List<KeyValuePair<EquipmentSlot, ItemStack>> Slots) : Packet<ClientGamePacketListener>
{
    //ContinueMask high-bit continue marker, maps to vanilla CONTINUE_MASK
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
