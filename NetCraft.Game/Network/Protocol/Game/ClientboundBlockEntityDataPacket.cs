using System.IO;
using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundBlockEntityDataPacket block entity data packet, maps to vanilla ClientboundBlockEntityDataPacket
//Fields: Pos (block position), BlockEntityTypeId (block entity type registry id), Tag (state NBT, may be null and treated as an empty compound when written)
//The field is not named TypeId: sharing the name with IPacket's network ID would be shadowed by the implicit implementation, sending the block entity type instead of the packet id
//NBT is the trailing field; written as a vanilla unnamed tag (type byte + payload, no root name) without an extra length prefix
public sealed record ClientboundBlockEntityDataPacket(BlockPos Pos, int BlockEntityTypeId, CompoundTag? Tag)
    : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundBlockEntityDataPacket> StreamCodec { get; } = new BlockEntityDataCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundBlockEntityData;

    public void Handle(ClientGamePacketListener handler) => handler.HandleBlockEntityData(this);

    private sealed class BlockEntityDataCodec : StreamCodec<FriendlyByteBuf, ClientboundBlockEntityDataPacket>
    {
        //Vanilla order: writeBlockPos -> VarInt typeId -> writeNbt
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

        //readNbt reads the trailing unnamed NBT; vanilla writes a single 0 byte to mean no data when null
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

        //writeNbt writes the trailing unnamed NBT
        //null must still be written as a valid empty compound (TAG_Compound + empty root name + TAG_End)
        //Vanilla decodes this field with ByteBufCodecs.COMPOUND_TAG; reading 0 bytes on the client throws as null
        //"Expected non-null compound tag" disconnects immediately, so writing only a single 0 is never allowed
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
