using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundContainerSetSlotPacket 容器槽位设置包对应原版 ClientboundContainerSetSlotPacket
//字段 ContainerId(VarInt) StateId(VarInt) Slot(Short) Stack(ItemStack)
//字段名 Stack 避免与 ItemStack 类型名冲突
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
