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

//LevelChunkSerializer 区块网络序列化器对应原版 net.minecraft.world.level.chunk.LevelChunk$Serializer
//C# partial 不能跨项目 NetCraft.Storage 不引用 NetCraft.Network
//改用独立静态类放 Game 层文件名 LevelChunk.Serializer.cs 保留原版语义
//S4 对齐 26.2 ClientboundLevelChunkPacketData 格式
//heightmaps map + varint bufferSize + buffer(sections) + blockEntities list
//section 含 short blockCount + short fluidCount palette entries 写全局 id
public static class LevelChunkSerializer
{
    //Write 写入区块数据到 FriendlyByteBuf 对应原版 LevelChunk.Serializer.write
    //S4 26.2 格式 heightmaps map(0 个) + buffer size varint + buffer + block entities
    //blockEntities 由调用方按区块取 每项是含 id 与坐标的完整 compound 客户端据此建本地方块实体
    public static void Write(FriendlyByteBuf buf, ChunkAccess chunk, Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory, IReadOnlyList<CompoundTag>? blockEntities = null)
    {
        //先序列化 sections 到临时 buffer 计算总长度
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

        //heightmaps map 空 0 个
        buf.WriteVarInt(0);
        //buffer 长度 + 数据
        buf.WriteVarInt(buffer.Length);
        buf.WriteBytes(buffer);
        //block entities 数量 + 每项原版 BlockEntityInfo
        //早期实现只写了 compound 客户端按原版三字段前缀解析会把 NBT 头读歪
        //表现为 Invalid tag id 直接断连 只要区块里有方块实体就必现
        var count = blockEntities?.Count ?? 0;
        buf.WriteVarInt(count);
        for (var i = 0; i < count; i++) WriteBlockEntityInfo(buf, blockEntities![i]);
    }

    //WriteBlockEntityInfo 写单个方块实体条目 对应原版 ClientboundLevelChunkPacketData$BlockEntityInfo
    //格式 byte packedXZ + short y + varint 类型号 + 无根名 compound
    //坐标与类型号由这三个独立字段传 compound 内部不再承担定位职责
    //packedXZ 位序与原版 create 一致 x 在高 4 位 z 在低 4 位
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

    //Read 从 FriendlyByteBuf 读取区块数据对应原版 ClientboundLevelChunkPacketData 解码
    //带回来的方块实体挂到 chunk.BlockEntityTags 由客户端消费
    public static LevelChunk Read(FriendlyByteBuf buf, ChunkPos pos, int minSectionY, int sectionsCount,
        Func<PalettedContainer<BlockState>> statesFactory, Func<PalettedContainer<Holder<Biome>>> biomesFactory,
        PalettedContainerFactory? factory = null)
    {
        factory ??= PalettedContainerFactory.Default;
        //heightmaps map 空跳过
        var heightmapCount = buf.ReadVarInt();
        for (var i = 0; i < heightmapCount; i++)
        {
            buf.ReadByte();
            var len = buf.ReadVarInt();
            for (var j = 0; j < len; j++) buf.ReadLong();
        }
        //buffer 长度 + 数据 解析为独立 buf
        var bufferSize = buf.ReadVarInt();
        var bufferBytes = buf.ReadBytes(bufferSize);
        var chunkBuf = new FriendlyByteBuf(bufferBytes);
        var chunk = new LevelChunk(pos, minSectionY, sectionsCount, statesFactory, biomesFactory);
        for (var i = 0; i < sectionsCount; i++)
        {
            var sectionY = minSectionY + i;
            ReadSectionInto(chunkBuf, chunk, sectionY, factory);
        }
        //block entities 数量 + 每项原版 BlockEntityInfo
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

    //ReadBlockEntityInfo 读单个方块实体条目并把坐标与类型号还原回 compound
    //还原是为了让 BlockEntityTypes.Load 仍按 id/x/y/z 反查 读端消费口径不变
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

    //WriteSection 写入单个区段对应原版 LevelChunkSection.write
    //S4 26.2 格式 short blockCount + short fluidCount + states + biomes
    private static void WriteSection(FriendlyByteBuf buf, LevelChunkSection section)
    {
        buf.WriteShort(section.NonEmptyBlockCount);
        buf.WriteShort(section.FluidCount);
        WritePalettedContainer(buf, section.States, WriteBlockState);
        WritePalettedContainer(buf, section.Biomes, WriteBiome);
    }

    //WriteEmptySection 空区段 air + plains 各 single value palette
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

    //ReadSectionInto 从 buf 读取区段数据写入 chunk
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

    //WritePalettedContainer 写入 PalettedContainer 网络序列化
    //S4 26.2 格式 bits byte + palette + storage(longs 无长度前缀)
    //直接写运行时 palette 不重排对应原版 PalettedContainer.write
    //bits=0 single value 写 1 个 entry 1<=bits<=8 写 varint count + entries 9+ global 不写
    //ClientboundChunksBiomesPacket 复用本方法故开放到程序集内
    internal static void WritePalettedContainer<T>(
        FriendlyByteBuf buf,
        PalettedContainer<T> container,
        Action<FriendlyByteBuf, T> writeElement)
    {
        var network = container.GetNetworkData();
        buf.WriteByte((byte)network.Bits);

        if (network.Bits == 0)
        {
            //single value palette 只写 1 个全局 id
            writeElement(buf, network.PaletteEntries[0]);
        }
        else if (network.Bits <= 8)
        {
            //linear 与 hashmap palette 都写 entries
            buf.WriteVarInt(network.PaletteEntries.Count);
            foreach (var entry in network.PaletteEntries)
                writeElement(buf, entry);
        }
        //bits 9+ global palette 不写 entries storage 即全局 id

        //storage longs 无长度前缀 客户端按 bits 与 entryCount 计算
        foreach (var v in network.RawStorage)
            buf.WriteLong(v);
    }

    //ReadPalettedContainer 读 palette 与 storage 后构造 PackedData 走 factory.Unpack 反序列化
    //storage 长度 = ceil(entryCount*bits/64) 无长度前缀
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
        //bits 9+ global palette 无 entries

        //storage 长度按 SimpleBitStorage 布局公式 valuesPerLong=64/bits下取整 非二的幂bits不能用bits*count/64
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

    //WriteBlockState 写全局 block state id 对齐原版 globalMap.getId
    private static void WriteBlockState(FriendlyByteBuf buf, BlockState state)
        => buf.WriteVarInt(state.Id);

    //WriteBiome 写全局 biome id 对齐原版 BuiltInRegistries.BIOME getId
    //ClientboundChunksBiomesPacket 复用本方法故开放到程序集内
    internal static void WriteBiome(FriendlyByteBuf buf, Holder<Biome> holder)
        => buf.WriteVarInt(Math.Max(0, BuiltInRegistries.BIOME.GetId(holder.Value)));

    //ReadBlockState 按 BlockStateRegistry 全局 id 反查 与 Write 对称
    //S4 不能用 BuiltInRegistries.BLOCK 查因 BlockStateRegistry 与 block 注册表 id 可能不一致
    private static BlockState ReadBlockState(FriendlyByteBuf buf, PalettedContainerFactory? factory = null)
        => BlockStateRegistry.GetState(buf.ReadVarInt());

    //ReadBiome 按全局 id 查 BIOME 注册表返回 Holder
    private static Holder<Biome> ReadBiome(FriendlyByteBuf buf, PalettedContainerFactory? factory = null)
    {
        var id = buf.ReadVarInt();
        var holder = BuiltInRegistries.BIOME.Get(id);
        return holder ?? Holder<Biome>.Direct(Biome.Plains);
    }
}
