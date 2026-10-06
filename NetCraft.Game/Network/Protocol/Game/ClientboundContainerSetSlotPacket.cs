using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundContainerSetSlotPacket container slot set packet, maps to vanilla ClientboundContainerSetSlotPacket
//Fields: ContainerId(VarInt), StateId(VarInt), Slot(Short), Stack(ItemStack)
//The field is named Stack to avoid clashing with the ItemStack type name
public sealed record ClientboundContainerSetSlotPacket(int ContainerId, int StateId, int Slot, ItemStack Stack) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundContainerSetSlotPacket> StreamCodec { get; } = new ContainerSetSlotCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundContainerSetSlot;

    public void Handle(ClientGamePacketListener handler) => handler.HandleContainerSetSlot(this);

    private sealed class ContainerSetSlotCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundContainerSetSlotPacket>
    {
        public ClientboundContainerSetSlotPacket Decode(RegistryFriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt(), buf.ReadShort(), ItemStack.OptionalStreamCodec.Decode(buf));

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundContainerSetSlotPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteVarInt(value.StateId);
            buf.WriteShort((short)value.Slot);
            ItemStack.OptionalStreamCodec.Encode(buf, value.Stack);
        }
    }
}
