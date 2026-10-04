using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//EntityDataSerializers 实体数据序列化器编号 对齐原版 EntityDataSerializers 的注册顺序
//本作只实现用到的三种 新增类型时编号必须与注册顺序一致
public static class EntityDataSerializers
{
    //Byte 单字节 编号 0 玩家共享标志位走这个
    public const int Byte = 0;

    //ItemStackId 物品栈 编号 7 掉落物持有的物品走这个
    public const int ItemStackId = 7;

    //Pose 姿态 编号 20 值是 Pose 枚举的 id
    public const int Pose = 20;
}

//EntityDataItem 实体数据条目对应原版 SynchedEntityData.DataValue
//Index 数据索引(按实体类 defineId 顺序) SerializerId 序列化器编号 Value 按编号解释
public sealed record EntityDataItem(byte Index, int SerializerId, object Value)
{
    //Byte 构造 BYTE 数据项
    public static EntityDataItem Byte(byte index, byte value) => new(index, EntityDataSerializers.Byte, value);

    //Pose 构造 POSE 数据项
    public static EntityDataItem Pose(byte index, int poseId) => new(index, EntityDataSerializers.Pose, poseId);

    //ItemStackData 构造 ITEM_STACK 数据项 掉落物持有的物品走这个
    public static EntityDataItem ItemStackData(byte index, ItemStack value)
        => new(index, EntityDataSerializers.ItemStackId, value);
}

//ClientboundSetEntityDataPacket 实体数据包对应原版 ClientboundSetEntityDataPacket
//字段 Id(int) PackedItems(List<SynchedEntityData.DataValue<?>>)
public sealed record ClientboundSetEntityDataPacket(int Id, IReadOnlyList<EntityDataItem> PackedItems)
    : Packet<ClientGamePacketListener>
{
    //EofMarker 条目结束标记 对应原版 EOF_MARKER
    public const byte EofMarker = 255;

    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEntityDataPacket> StreamCodec { get; } = new SetEntityDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetEntityData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetEntityData(this);

    private sealed class SetEntityDataCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundSetEntityDataPacket>
    {
        //原版 unpack 逐条读 index(单字节) + serializerId(VarInt) + 值 读到 255 结束
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

        //原版 pack 每条 index + serializerId + 值 末尾补 255
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

        //ReadValue 按序列化器编号读取值 未实现的编号无法定位后续字节只能抛错
        //BYTE 分支显式装箱 否则 switch 会把 byte 提升成 int 导致取出的值是 Int32
        private static object ReadValue(RegistryFriendlyByteBuf buf, int serializerId) => serializerId switch
        {
            EntityDataSerializers.Byte => (object)buf.ReadByte(),
            EntityDataSerializers.ItemStackId => ItemStack.OptionalStreamCodec.Decode(buf),
            EntityDataSerializers.Pose => buf.ReadVarInt(),
            _ => throw new NotSupportedException($"未实现的实体数据序列化器编号 {serializerId}"),
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
                    throw new NotSupportedException($"未实现的实体数据序列化器编号 {item.SerializerId}");
            }
        }
    }
}
