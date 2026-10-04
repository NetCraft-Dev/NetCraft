using System.IO;
using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockEntityDataPacket 方块实体数据包对应原版 ClientboundBlockEntityDataPacket
//字段 Pos(方块坐标) BlockEntityTypeId(方块实体类型注册表序号) Tag(状态 NBT 可为 null 写出时按空 compound 处理)
//字段不叫 TypeId: 与 IPacket 的网络 ID 同名会被隐式实现盖掉 发出去的是方块实体类型而不是包 id
//NBT 是包尾字段 按原版写 unnamed tag(type byte + payload 无 root name) 不额外加长度前缀
public sealed record ClientboundBlockEntityDataPacket(BlockPos Pos, int BlockEntityTypeId, CompoundTag? Tag)
    : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockEntityDataPacket> StreamCodec { get; } = new BlockEntityDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockEntityData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockEntityData(this);

    private sealed class BlockEntityDataCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockEntityDataPacket>
    {
        //原版顺序 writeBlockPos -> VarInt typeId -> writeNbt
        public ClientboundBlockEntityDataPacket Decode(FriendlyByteBuf buf)
        {
            var pos = buf.ReadBlockPos();
            var typeId = buf.ReadVarInt();
            return new ClientboundBlockEntityDataPacket(pos, typeId, ReadNbt(buf));
        }

        public void Encode(FriendlyByteBuf buf, ClientboundBlockEntityDataPacket value)
        {
            buf.WriteBlockPos(value.Pos);
            buf.WriteVarInt(value.BlockEntityTypeId);
            buf.WriteBytes(WriteNbt(value.Tag));
        }

        //readNbt 读包尾的 unnamed NBT 原版写 null 时只写一个 0 字节表示无数据
        private static CompoundTag? ReadNbt(FriendlyByteBuf buf)
        {
            var remaining = buf.ReadableBytes;
            if (remaining <= 0) return null;
            var data = buf.ReadBytes(remaining);
            if (data.Length == 1 && data[0] == 0) return null;
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            return NbtIo.ReadAnyTag(new BinaryNbtReader(reader), new NbtAccounter()) as CompoundTag;
        }

        //writeNbt 写包尾的 unnamed NBT
        //null 也要写成合法的空 compound(TAG_Compound + 空根名 + TAG_End)
        //原版这个字段用 ByteBufCodecs.COMPOUND_TAG 编解码 客户端读到 0 字节会当 null 抛
        //Expected non-null compound tag 直接断连 所以绝不能只写一个 0
        private static byte[] WriteNbt(CompoundTag? tag)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            NbtIo.WriteAnyTag(tag ?? new CompoundTag(), new BinaryNbtWriter(writer));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
