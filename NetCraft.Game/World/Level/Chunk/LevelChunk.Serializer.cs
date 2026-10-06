using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Nbt;
using NetCraft.Network;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;

namespace NetCraft.Game.World.Level.Chunk;

//LevelChunkSerializer chunk network serializer, maps to vanilla net.minecraft.world.level.chunk.LevelChunk$Serializer
//C# partial cannot span projects; NetCraft.Storage does not reference NetCraft.Network
//Instead a standalone static class in the Game layer, file name LevelChunk.Serializer.cs, keeps the vanilla semantics
//S4 aligns with the 26.2 ClientboundLevelChunkPacketData format
//heightmaps map + varint bufferSize + buffer(sections) + blockEntities list
//section contains short blockCount + short fluidCount; palette entries write global ids
public static class LevelChunkSerializer
{
    //Write writes chunk data to FriendlyByteBuf, maps to vanilla LevelChunk.Serializer.write
    //S4 26.2 format: heightmaps map (0 entries) + buffer size varint + buffer + block entities
    //blockEntities are supplied per chunk by the caller; each is a full compound with id and coordinates, from which the client builds local block entities
    public static void Write(FriendlyByteBuf buf, ChunkAccess chunk, Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory, IReadOnlyList<CompoundTag>? blockEntities = null)
    {
        //Serialize sections into a temporary buffer first to compute the total length
        var tmp = new FriendlyByteBuf();
        for (var i = 0; i < chunk.SectionsCount; i++)
        {
            var sectionY = chunk.MinSectionY + i;
            var section = chunk.GetSection(sectionY);
            if (section is null)
                WriteEmptySection(tmp);
            else
                WriteSection(tmp, section);
        }
        var buffer = tmp.ToArray();

        //heightmaps map empty, 0 entries
        buf.WriteVarInt(0);
        //buffer length + data
        buf.WriteVarInt(buffer.Length);
        buf.WriteBytes(buffer);
        //block entities count + one vanilla BlockEntityInfo each
        //An earlier implementation wrote only the compound; the client parsed it with the vanilla three-field prefix and misread the NBT header
        //Manifesting as Invalid tag id and an immediate disconnect, always reproducible when a chunk contains a block entity
        var count = blockEntities?.Count ?? 0;
        buf.WriteVarInt(count);
        for (var i = 0; i < count; i++) WriteBlockEntityInfo(buf, blockEntities![i]);
    }

    //WriteBlockEntityInfo writes a single block entity entry, maps to vanilla ClientboundLevelChunkPacketData$BlockEntityInfo
    //Format: byte packedXZ + short y + varint type id + rootless compound
    //Coordinates and type id are carried by these three separate fields; the compound no longer handles positioning
    //packedXZ bit order matches vanilla create: x in the high 4 bits, z in the low 4 bits
    private static void WriteBlockEntityInfo(FriendlyByteBuf buf, CompoundTag tag)
    {
        var x = tag.GetIntOr("x", 0);
        var y = tag.GetIntOr("y", 0);
        var z = tag.GetIntOr("z", 0);
        buf.WriteByte((byte)(((x & 15) << 4) | (z & 15)));
        buf.WriteShort((short)y);
        var type = tag.GetString("id")?.Value is { } key
            ? NetCraft.Game.World.Level.Block.BlockEntityTypes.ByKey(key)
            : null;
        buf.WriteVarInt(type?.RawId ?? 0);
        buf.WriteNbt(tag);
    }

    //Read reads chunk data from FriendlyByteBuf, maps to vanilla ClientboundLevelChunkPacketData decoding
    //The block entities brought back are attached to chunk.BlockEntityTags and consumed by the client
    public static LevelChunk Read(FriendlyByteBuf buf, ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory,
        PalettedContainerFactory? factory = null)
    {
        factory ??= PalettedContainerFactory.Default;
        //heightmaps map empty, skip
        var heightmapCount = buf.ReadVarInt();
        for (var i = 0; i < heightmapCount; i++)
        {
            buf.ReadByte();
            var len = buf.ReadVarInt();
            for (var j = 0; j < len; j++) buf.ReadLong();
        }
        //buffer length + data, parsed into a separate buf
        var bufferSize = buf.ReadVarInt();
        var bufferBytes = buf.ReadBytes(bufferSize);
        var chunkBuf = new FriendlyByteBuf(bufferBytes);
        var chunk = new LevelChunk(pos, minSectionY, sectionsCount, statesFactory, biomesFactory);
        for (var i = 0; i < sectionsCount; i++)
        {
            var sectionY = minSectionY + i;
            ReadSectionInto(chunkBuf, chunk, sectionY, factory);
        }
        //block entities count + one vanilla BlockEntityInfo each
        var blockEntityCount = buf.ReadVarInt();
        if (blockEntityCount > 0)
        {
            var tags = new List<CompoundTag>(blockEntityCount);
            for (var i = 0; i < blockEntityCount; i++)
                if (ReadBlockEntityInfo(buf, pos) is { } tag) tags.Add(tag);
            chunk.BlockEntityTags = tags;
        }
        return chunk;
    }

    //ReadBlockEntityInfo reads a single block entity entry and restores coordinates and type id back into the compound
    //Restoring keeps BlockEntityTypes.Load looking up by id/x/y/z, so the reader-side consumption contract is unchanged
    private static CompoundTag? ReadBlockEntityInfo(FriendlyByteBuf buf, ChunkPos pos)
    {
        var packedXZ = buf.ReadByte();
        var y = buf.ReadShort();
        var typeId = buf.ReadVarInt();
        if (buf.ReadNbt() is not CompoundTag tag) return null;
        if (NetCraft.Game.World.Level.Block.BlockEntityTypes.ById(typeId) is { } type)
            tag.PutString("id", type.Id.ToString());
        tag.PutInt("x", (pos.X << 4) | ((packedXZ >> 4) & 15));
        tag.PutInt("y", y);
        tag.PutInt("z", (pos.Z << 4) | (packedXZ & 15));
        return tag;
    }

    //WriteSection writes a single section, maps to vanilla LevelChunkSection.write
    //S4 26.2 format: short blockCount + short fluidCount + states + biomes
    private static void WriteSection(FriendlyByteBuf buf, LevelChunkSection section)
    {
        buf.WriteShort(section.NonEmptyBlockCount);
        buf.WriteShort(section.FluidCount);
        WritePalettedContainer(buf, section.States, WriteBlockState);
        WritePalettedContainer(buf, section.Biomes, WriteBiome);
    }

    //WriteEmptySection empty section, air + plains as single-value palettes
    private static void WriteEmptySection(FriendlyByteBuf buf)
    {
        buf.WriteShort(0);
        buf.WriteShort(0);
        //states bits=0 single value air(id=0)
        buf.WriteByte(0);
        buf.WriteVarInt(0);
        //biomes bits=0 single value plains(id=0)
        buf.WriteByte(0);
        buf.WriteVarInt(0);
    }

    //ReadSectionInto reads section data from buf into chunk
    private static void ReadSectionInto(FriendlyByteBuf buf, LevelChunk chunk, int sectionY,
        PalettedContainerFactory factory)
    {
        _ = buf.ReadShort();
        _ = buf.ReadShort();
        var states = ReadPalettedContainer<BlockState>(buf, factory.UnpackBlockStates, b => ReadBlockState(b), 4096);
        var biomes = ReadPalettedContainer<Holder<Biome>>(buf, factory.UnpackBiomes, b => ReadBiome(b), 64);
        var section = new LevelChunkSection(states, biomes);
        chunk.SetSection(sectionY, section);
    }

    //WritePalettedContainer writes PalettedContainer network serialization
    //S4 26.2 format: bits byte + palette + storage (longs without length prefix)
    //Writes the runtime palette directly without reordering, maps to vanilla PalettedContainer.write
    //bits=0 single value writes 1 entry; 1<=bits<=8 writes varint count + entries; 9+ global writes none
    //ClientboundChunksBiomesPacket reuses this method, hence internal to the assembly
    internal static void WritePalettedContainer<T>(
        FriendlyByteBuf buf,
        PalettedContainer<T> container,
        Action<FriendlyByteBuf, T> writeElement)
    {
        var network = container.GetNetworkData();
        buf.WriteByte((byte)network.Bits);

        if (network.Bits == 0)
        {
            //single value palette writes only 1 global id
            writeElement(buf, network.PaletteEntries[0]);
        }
        else if (network.Bits <= 8)
        {
            //both linear and hashmap palettes write entries
            buf.WriteVarInt(network.PaletteEntries.Count);
            foreach (var entry in network.PaletteEntries)
                writeElement(buf, entry);
        }
        //bits 9+ global palette writes no entries; the storage itself holds global ids

        //storage longs without length prefix; the client derives the length from bits and entryCount
        foreach (var v in network.RawStorage)
            buf.WriteLong(v);
    }

    //ReadPalettedContainer reads palette and storage then builds PackedData and deserializes through factory.Unpack
    //storage length = ceil(entryCount*bits/64), no length prefix
    private static PalettedContainer<T> ReadPalettedContainer<T>(
        FriendlyByteBuf buf,
        Func<PackedData<T>, PalettedContainer<T>> unpack,
        Func<FriendlyByteBuf, T> readElement,
        int entryCount)
    {
        var bits = buf.ReadByte();
        var palette = new List<T>();
        if (bits == 0)
        {
            palette.Add(readElement(buf));
        }
        else if (bits <= 8)
        {
            var paletteCount = buf.ReadVarInt();
            for (var i = 0; i < paletteCount; i++)
                palette.Add(readElement(buf));
        }
        //bits 9+ global palette has no entries

        //storage length follows the SimpleBitStorage layout formula valuesPerLong=64/bits (floored); for non-power-of-two bits, bits*count/64 does not work
        var storageCount = 0;
        if (bits != 0)
        {
            var valuesPerLong = 64 / bits;
            storageCount = (entryCount + valuesPerLong - 1) / valuesPerLong;
        }
        long[] storage = new long[storageCount];
        for (var i = 0; i < storageCount; i++)
            storage[i] = buf.ReadLong();

        var packed = new PackedData<T>(palette, Optional<long[]>.Of(storage), bits);
        return unpack(packed);
    }

    //WriteBlockState writes the global block state id, aligns with vanilla globalMap.getId
    private static void WriteBlockState(FriendlyByteBuf buf, BlockState state)
        => buf.WriteVarInt(state.Id);

    //WriteBiome writes the global biome id, aligns with vanilla BuiltInRegistries.BIOME getId
    //ClientboundChunksBiomesPacket reuses this method, hence internal to the assembly
    internal static void WriteBiome(FriendlyByteBuf buf, Holder<Biome> holder)
        => buf.WriteVarInt(Math.Max(0, BuiltInRegistries.BIOME.GetId(holder.Value)));

    //ReadBlockState looks up by BlockStateRegistry global id, symmetric with Write
    //S4 cannot use BuiltInRegistries.BLOCK because BlockStateRegistry and the block registry ids may differ
    private static BlockState ReadBlockState(FriendlyByteBuf buf, PalettedContainerFactory? factory = null)
        => BlockStateRegistry.GetState(buf.ReadVarInt());

    //ReadBiome looks up the BIOME registry by global id and returns a Holder
    private static Holder<Biome> ReadBiome(FriendlyByteBuf buf, PalettedContainerFactory? factory = null)
    {
        var id = buf.ReadVarInt();
        var holder = BuiltInRegistries.BIOME.Get(id);
        return holder ?? Holder<Biome>.Direct(Biome.Plains);
    }
}
