using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//EntityDataSerializers entity data serializer ids, aligns with the registration order of vanilla EntityDataSerializers
//This project implements only the three used here; when adding a type the id must match the registration order
public static class EntityDataSerializers
{
    //Byte single byte, id 0; the player shared flags use this
    public const int Byte = 0;

    //ItemStackId item stack, id 7; the item held by a dropped item uses this
    public const int ItemStackId = 7;

    //Pose pose, id 20; the value is the id of the Pose enum
    public const int Pose = 20;
}

//EntityDataItem entity data item, maps to vanilla SynchedEntityData.DataValue
//Index data index (in the order of the entity class's defineId), SerializerId serializer id, Value interpreted by that id
public sealed record EntityDataItem(byte Index, int SerializerId, object Value)
{
    //Byte constructs a BYTE data item
    public static EntityDataItem Byte(byte index, byte value) => new(index, EntityDataSerializers.Byte, value);

    //Pose constructs a POSE data item
    public static EntityDataItem Pose(byte index, int poseId) => new(index, EntityDataSerializers.Pose, poseId);

    //ItemStackData constructs an ITEM_STACK data item; the item held by a dropped item uses this
    public static EntityDataItem ItemStackData(byte index, ItemStack value)
        => new(index, EntityDataSerializers.ItemStackId, value);
}

//ClientboundSetEntityDataPacket entity data packet, maps to vanilla ClientboundSetEntityDataPacket
//Fields: Id(int), PackedItems(List<SynchedEntityData.DataValue<?>>)
public sealed record ClientboundSetEntityDataPacket(int Id, IReadOnlyList<EntityDataItem> PackedItems)
    : Packet<ClientGamePacketListener>
{
    //EofMarker entry end marker, maps to vanilla EOF_MARKER
    public const byte EofMarker = 255;

    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEntityDataPacket> StreamCodec { get; } = new SetEntityDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityData(this);

    private sealed class SetEntityDataCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEntityDataPacket>
    {
        //Vanilla unpack reads index (single byte) + serializerId (VarInt) + value per entry, ending at 255
        public ClientboundSetEntityDataPacket Decode(RegistryFriendlyByteBuf buf)
        {
            var id = buf.ReadVarInt();
            var items = new List<EntityDataItem>();
            while (true)
            {
                var index = buf.ReadByte();
                if (index == EofMarker) break;
                var serializerId = buf.ReadVarInt();
                items.Add(new EntityDataItem(index, serializerId, ReadValue(buf, serializerId)));
            }
            return new ClientboundSetEntityDataPacket(id, items);
        }

        //Vanilla pack writes index + serializerId + value per entry and appends 255
        public void Encode(RegistryFriendlyByteBuf buf, ClientboundSetEntityDataPacket value)
        {
            buf.WriteVarInt(value.Id);
            foreach (var item in value.PackedItems)
            {
                buf.WriteByte(item.Index);
                buf.WriteVarInt(item.SerializerId);
                WriteValue(buf, item);
            }
            buf.WriteByte(EofMarker);
        }

        //ReadValue reads a value by serializer id; an unimplemented id cannot locate the following bytes, so it must throw
        //The BYTE branch boxes explicitly; otherwise the switch would promote byte to int and the value would come out as Int32
        private static object ReadValue(RegistryFriendlyByteBuf buf, int serializerId) => serializerId switch
        {
            EntityDataSerializers.Byte => (object)buf.ReadByte(),
            EntityDataSerializers.ItemStackId => ItemStack.OptionalStreamCodec.Decode(buf),
            EntityDataSerializers.Pose => buf.ReadVarInt(),
            _ => throw new NotSupportedException($"Unimplemented entity data serializer id {serializerId}"),
        };

        private static void WriteValue(RegistryFriendlyByteBuf buf, EntityDataItem item)
        {
            switch (item.SerializerId)
            {
                case EntityDataSerializers.Byte:
                    buf.WriteByte((byte)item.Value);
                    return;
                case EntityDataSerializers.ItemStackId:
                    ItemStack.OptionalStreamCodec.Encode(buf, (ItemStack)item.Value);
                    return;
                case EntityDataSerializers.Pose:
                    buf.WriteVarInt((int)item.Value);
                    return;
                default:
                    throw new NotSupportedException($"Unimplemented entity data serializer id {item.SerializerId}");
            }
        }
    }
}
