using NetCraft.Game.World.Inventory;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundContainerClickPacket container click packet, maps to vanilla ServerboundContainerClickPacket
//Fields: ContainerId(VarInt), StateId(VarInt), SlotNum(Short), ButtonNum(Byte), Input(VarInt id)
//ChangedSlots is a map from slot number (Short) to HashedStack, with a vanilla cap of 128 entries
//CarriedItem is the summary of the stack held by the cursor
//The field is named Input to avoid clashing with the ContainerInput type name
public sealed record ServerboundContainerClickPacket(
    int ContainerId,
    int StateId,
    short SlotNum,
    byte ButtonNum,
    ContainerInput Input,
    Dictionary<int, HashedStack> ChangedSlots,
    HashedStack CarriedItem) : Packet<ServerGamePacketListener>
{
    //MaxSlotCount changed slot count cap, maps to vanilla 128
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
                throw new InvalidOperationException($"Container click changed slots exceed the limit: {count}");
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
