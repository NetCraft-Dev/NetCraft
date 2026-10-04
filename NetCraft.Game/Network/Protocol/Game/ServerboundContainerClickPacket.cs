using NetCraft.Game.World.Inventory;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerClickPacket 容器点击包对应原版 ServerboundContainerClickPacket
//字段 ContainerId(VarInt) StateId(VarInt) SlotNum(Short) ButtonNum(Byte) Input(VarInt id)
//ChangedSlots 为槽号(Short)到 HashedStack 的映射 原版上限 128 项
//CarriedItem 为鼠标拖着的栈摘要
//字段名 Input 避免与 ContainerInput 类型名冲突
public sealed record ServerboundContainerClickPacket(
    int ContainerId,
    int StateId,
    short SlotNum,
    byte ButtonNum,
    ContainerInput Input,
    Dictionary<int, HashedStack> ChangedSlots,
    HashedStack CarriedItem) : Packet<ServerGamePacketListener>
{
    //MaxSlotCount 变更槽位数量上限对应原版 128
    public const int MaxSlotCount = 128;

    public static StreamCodec<RegistryFriendlyByteBuf, ServerboundContainerClickPacket> StreamCodec { get; } = new ContainerClickCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundContainerClick;

    public void Handle(ServerGamePacketListener handler) => handler.HandleContainerClick(this);

    private sealed class ContainerClickCodec : StreamCodec<RegistryFriendlyByteBuf, ServerboundContainerClickPacket>
    {
        public ServerboundContainerClickPacket Decode(RegistryFriendlyByteBuf buf)
        {
            int containerId = buf.ReadVarInt();
            int stateId = buf.ReadVarInt();
            short slotNum = buf.ReadShort();
            byte buttonNum = buf.ReadByte();
            var input = ContainerInputCodec.StreamCodec.Decode(buf);

            int count = buf.ReadVarInt();
            if (count > MaxSlotCount)
                throw new InvalidOperationException($"容器点击变更槽位超限: {count}");
            var changedSlots = new Dictionary<int, HashedStack>(Math.Min(count, ByteBufCodecs.MaxInitialCollectionSize));
            for (int i = 0; i < count; i++)
            {
                int slot = buf.ReadShort();
                changedSlots[slot] = HashedStack.StreamCodec.Decode(buf);
            }

            var carriedItem = HashedStack.StreamCodec.Decode(buf);
            return new(containerId, stateId, slotNum, buttonNum, input, changedSlots, carriedItem);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ServerboundContainerClickPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteVarInt(value.StateId);
            buf.WriteShort(value.SlotNum);
            buf.WriteByte(value.ButtonNum);
            ContainerInputCodec.StreamCodec.Encode(buf, value.Input);

            buf.WriteVarInt(value.ChangedSlots.Count);
            foreach (var entry in value.ChangedSlots)
            {
                buf.WriteShort((short)entry.Key);
                HashedStack.StreamCodec.Encode(buf, entry.Value);
            }

            HashedStack.StreamCodec.Encode(buf, value.CarriedItem);
        }
    }
}
