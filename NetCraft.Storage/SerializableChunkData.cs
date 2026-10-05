using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Storage.Paletted;
using NetCraft.Storage.Ticks;

namespace NetCraft.Storage;

//SerializableChunkData对应原版net.minecraft.world.level.chunk.storage.SerializableChunkData
//区块数据序列化中间表示write写为CompoundTag parse从CompoundTag反序列化
//copyOf/read依赖ServerLevel等游戏内容未实现抛NotSupportedException
public sealed class SerializableChunkData
{
    public const string XPosTag = "xPos";
    public const string ZPosTag = "zPos";
    public const string HeightmapsTag = "Heightmaps";
    public const string IsLightOnTag = "isLightOn";
    public const string SectionsTag = "sections";
    public const string BlockLightTag = "BlockLight";
    public const string SkyLightTag = "SkyLight";
    private const string TagUpgradeData = "UpgradeData";
    private const string BlockTicksTag = "block_ticks";
    private const string FluidTicksTag = "fluid_ticks";
    private const string YPosTag = "yPos";
    private const string LastUpdateTag = "LastUpdate";
    private const string InhabitedTimeTag = "InhabitedTime";
    private const string StatusTag = "Status";
    private const string BlendingDataTag = "blending_data";
    private const string BelowZeroRetrogenTag = "below_zero_retrogen";
    private const string CarvingMaskTag = "carving_mask";
    private const string PostProcessingTag = "PostProcessing";
    private const string BlockEntitiesTag = "block_entities";
    private const string EntitiesTag = "entities";
    private const string StructuresTag = "structures";

    public PalettedContainerFactory ContainerFactory { get; }
    public ChunkPos ChunkPos { get; }
    public int MinSectionY { get; }
    public long LastUpdateTime { get; }
    public long InhabitedTime { get; }
    public ChunkStatus ChunkStatus { get; }
    public BlendingData.Packed? BlendingData { get; }
    public BelowZeroRetrogen? BelowZeroRetrogen { get; }
    public UpgradeData UpgradeData { get; }
    public long[]? CarvingMask { get; }
    public IDictionary<Heightmap.Types, long[]> Heightmaps { get; }
    public PackedTicks PackedTicks { get; }
    public List<short>?[] PostProcessingSections { get; }
    public bool LightCorrect { get; }
    public List<SectionData> SectionDataList { get; }
    public List<CompoundTag> Entities { get; }
    public List<CompoundTag> BlockEntities { get; }
    public CompoundTag StructureData { get; }

    public SerializableChunkData(
        PalettedContainerFactory containerFactory,
        ChunkPos chunkPos,
        int minSectionY,
        long lastUpdateTime,
        long inhabitedTime,
        ChunkStatus chunkStatus,
        BlendingData.Packed? blendingData,
        BelowZeroRetrogen? belowZeroRetrogen,
        UpgradeData upgradeData,
        long[]? carvingMask,
        IDictionary<Heightmap.Types, long[]> heightmaps,
        PackedTicks packedTicks,
        List<short>?[] postProcessingSections,
        bool lightCorrect,
        List<SectionData> sectionData,
        List<CompoundTag> entities,
        List<CompoundTag> blockEntities,
        CompoundTag structureData)
    {
        ContainerFactory = containerFactory;
        ChunkPos = chunkPos;
        MinSectionY = minSectionY;
        LastUpdateTime = lastUpdateTime;
        InhabitedTime = inhabitedTime;
        ChunkStatus = chunkStatus;
        BlendingData = blendingData;
        BelowZeroRetrogen = belowZeroRetrogen;
        UpgradeData = upgradeData;
        CarvingMask = carvingMask;
        Heightmaps = heightmaps;
        PackedTicks = packedTicks;
        PostProcessingSections = postProcessingSections;
        LightCorrect = lightCorrect;
        SectionDataList = sectionData;
        Entities = entities;
        BlockEntities = blockEntities;
        StructureData = structureData;
    }

    //SectionData对应原版SerializableChunkData.SectionData
    //区段y与LevelChunkSection及光照数据打包
    public sealed record SectionData(int Y, LevelChunkSection? ChunkSection, DataLayer? BlockLight, DataLayer? SkyLight);

    //write对应原版SerializableChunkData.write
    //将所有字段序列化为CompoundTag用于写入MCA文件
    public CompoundTag Write()
    {
        //Log.Debug($"Write 入口");
        var tag = NbtUtils.AddCurrentDataVersion(new CompoundTag());
        tag.PutInt(XPosTag, ChunkPos.X);
        tag.PutInt(YPosTag, MinSectionY);
        tag.PutInt(ZPosTag, ChunkPos.Z);
        tag.PutLong(LastUpdateTag, LastUpdateTime);
        tag.PutLong(InhabitedTimeTag, InhabitedTime);
        tag.PutString(StatusTag, ChunkStatus.Name);
        if (BlendingData is not null) tag.Put(BlendingDataTag, (CompoundTag)BlendingData.Data.Copy());
        if (BelowZeroRetrogen is not null) tag.Put(BelowZeroRetrogenTag, (CompoundTag)BelowZeroRetrogen.Data.Copy());
        if (!UpgradeData.IsEmpty()) tag.Put(TagUpgradeData, UpgradeData.Write());

        var sectionTags = new ListTag();
        var blockStatesCodec = ContainerFactory.BlockStatesContainerCodec();
        var biomeCodec = ContainerFactory.BiomeContainerCodec();
        foreach (var section in SectionDataList)
        {
            var sectionTag = new CompoundTag();
            if (section.ChunkSection is not null)
            {
                sectionTag.Store("block_states", blockStatesCodec, section.ChunkSection.States);
                sectionTag.Store("biomes", biomeCodec, section.ChunkSection.Biomes);
            }
            if (section.BlockLight is not null) sectionTag.PutByteArray(BlockLightTag, section.BlockLight.GetData());
            if (section.SkyLight is not null) sectionTag.PutByteArray(SkyLightTag, section.SkyLight.GetData());
            if (!sectionTag.IsEmpty)
            {
                sectionTag.PutByte("Y", (byte)section.Y);
                sectionTags.Add(sectionTag);
            }
        }
        tag.Put(SectionsTag, sectionTags);

        if (LightCorrect) tag.PutBoolean(IsLightOnTag, true);

        var blockEntityTags = new ListTag();
        foreach (var be in BlockEntities) blockEntityTags.Add(be);
        tag.Put(BlockEntitiesTag, blockEntityTags);

        if (ChunkStatus.GetChunkType() == ChunkType.ProtoChunk)
        {
            var entityTags = new ListTag();
            foreach (var e in Entities) entityTags.Add(e);
            tag.Put(EntitiesTag, entityTags);
            if (CarvingMask is not null) tag.PutLongArray(CarvingMaskTag, CarvingMask);
        }

        SaveTicks(tag, PackedTicks);
        tag.Put(PostProcessingTag, PackOffsets(PostProcessingSections));

        var heightmapsTag = new CompoundTag();
        foreach (var (type, data) in Heightmaps)
            heightmapsTag.PutLongArray(type.GetSerializationKey(), data);
        tag.Put(HeightmapsTag, heightmapsTag);

        tag.Put(StructuresTag, (CompoundTag)StructureData.Copy());
        //Log.Debug($"Write 出口 pos={ChunkPos} sections={SectionDataList.Count}");
        return tag;
    }

    //saveTicks对应原版saveTicks
    //stub化为ListTag of CompoundTag保留原始tick数据
    private static void SaveTicks(CompoundTag tag, PackedTicks ticks)
    {
        var blockTicks = new ListTag();
        foreach (var t in ticks.Blocks) blockTicks.Add(t);
        tag.Put(BlockTicksTag, blockTicks);

        var fluidTicks = new ListTag();
        foreach (var t in ticks.Fluids) fluidTicks.Add(t);
        tag.Put(FluidTicksTag, fluidTicks);
    }

    //packOffsets对应原版packOffsets
    //每个ShortList转为ListTag of ShortTag
    private static ListTag PackOffsets(List<short>?[] postProcessingSections)
    {
        var list = new ListTag();
        foreach (var shorts in postProcessingSections)
        {
            var inner = new ListTag();
            if (shorts is not null)
                foreach (var s in shorts) inner.Add(new ShortTag(s));
            list.Add(inner);
        }
        return list;
    }

    //parse对应原版SerializableChunkData.parse
    //从CompoundTag反序列化区块数据返回null表示无Status字段
    public static SerializableChunkData? Parse(LevelHeightAccessor levelHeight, PalettedContainerFactory containerFactory, CompoundTag chunkData)
    {
        //Log.Debug($"Parse 入口 levelHeight={levelHeight} containerFactory={containerFactory} chunkData={chunkData}");
        //Log.Debug($"Parse 入口 levelHeight={levelHeight} containerFactory={containerFactory}");
        if (string.IsNullOrEmpty(chunkData.GetStringValue(StatusTag)))
        {
            //Log.Debug($"Parse 出口 result=null");
            return null;
        }

        var chunkPos = new ChunkPos(chunkData.GetIntOr(XPosTag, 0), chunkData.GetIntOr(ZPosTag, 0));
        var lastUpdateTime = chunkData.GetLongOr(LastUpdateTag, 0L);
        var inhabitedTime = chunkData.GetLongOr(InhabitedTimeTag, 0L);
        var status = chunkData.Read(StatusTag, ChunkStatus.Codec).OrElse(ChunkStatus.EMPTY);
        var upgradeDataTag = chunkData.GetCompound(TagUpgradeData);
        var upgradeData = upgradeDataTag is not null ? new UpgradeData(upgradeDataTag) : UpgradeData.Empty;
        var lightCorrect = chunkData.GetBooleanOr(IsLightOnTag, false);
        var blendingDataTag = chunkData.GetCompound(BlendingDataTag);
        var blendingData = blendingDataTag is not null ? new BlendingData.Packed(blendingDataTag) : null;
        var belowZeroRetrogenTag = chunkData.GetCompound(BelowZeroRetrogenTag);
        var belowZeroRetrogen = belowZeroRetrogenTag is not null ? new BelowZeroRetrogen(belowZeroRetrogenTag) : null;
        var carvingMaskArray = chunkData.GetLongArray(CarvingMaskTag);
        var carvingMask = carvingMaskArray?.Value;

        var heightmaps = new Dictionary<Heightmap.Types, long[]>();
        var heightmapsTag = chunkData.GetCompound(HeightmapsTag);
        if (heightmapsTag is not null)
        {
            foreach (var type in status.HeightmapsAfter())
            {
                var data = heightmapsTag.GetLongArray(type.GetSerializationKey());
                if (data is not null) heightmaps[type] = data.Value;
            }
        }

        var packedTicks = new PackedTicks();
        var blockTicksList = chunkData.GetList(BlockTicksTag);
        if (blockTicksList is not null)
            foreach (var t in blockTicksList)
                if (t is CompoundTag c) packedTicks.Blocks.Add(c);
        var fluidTicksList = chunkData.GetList(FluidTicksTag);
        if (fluidTicksList is not null)
            foreach (var t in fluidTicksList)
                if (t is CompoundTag c) packedTicks.Fluids.Add(c);

        var postProcessTags = chunkData.GetListOrEmpty(PostProcessingTag);
        var postProcessingSections = new List<short>?[postProcessTags.Count];
        for (var sectionIndex = 0; sectionIndex < postProcessTags.Count; sectionIndex++)
        {
            var offsetsTag = postProcessTags.GetList(sectionIndex);
            if (offsetsTag is not null && !offsetsTag.IsEmpty)
            {
                var shorts = new List<short>(offsetsTag.Count);
                for (var i = 0; i < offsetsTag.Count; i++)
                    shorts.Add(offsetsTag.GetShort(i)?.Value ?? (short)0);
                postProcessingSections[sectionIndex] = shorts;
            }
        }

        var entities = new List<CompoundTag>();
        var entitiesList = chunkData.GetList(EntitiesTag);
        if (entitiesList is not null)
            foreach (var t in entitiesList)
                if (t is CompoundTag c) entities.Add(c);

        var blockEntities = new List<CompoundTag>();
        var blockEntitiesList = chunkData.GetList(BlockEntitiesTag);
        if (blockEntitiesList is not null)
            foreach (var t in blockEntitiesList)
                if (t is CompoundTag c) blockEntities.Add(c);

        var structureData = chunkData.GetCompoundOrEmpty(StructuresTag);

        var sectionTags = chunkData.GetListOrEmpty(SectionsTag);
        var sectionData = new List<SectionData>(sectionTags.Count);
        var blockStatesCodec = containerFactory.BlockStatesContainerCodec();
        var biomeCodec = containerFactory.BiomeContainerCodec();
        for (var i = 0; i < sectionTags.Count; i++)
        {
            var maybeSectionTag = sectionTags.GetCompound(i);
            if (maybeSectionTag is null || maybeSectionTag.IsEmpty) continue;
            var sectionTag = maybeSectionTag;
            //anvil 的 Y 是有符号 byte 负区段按补码写 必须按 sbyte 解释否则会被判为越界丢弃
            var y = (sbyte)sectionTag.GetByteOr("Y", 0);

            LevelChunkSection? section;
            if (y >= levelHeight.MinSectionY && y <= levelHeight.MaxSectionY)
            {
                var blocksContainer = sectionTag.GetCompound("block_states");
                PalettedContainer<BlockState>? blocks = null;
                if (blocksContainer is not null)
                {
                    var result = blockStatesCodec.Parse(NbtOps.Instance, blocksContainer);
                    blocks = result.GetOrThrow(msg => new ChunkReadException(msg));
                }
                blocks ??= containerFactory.CreateForBlockStates();

                var biomesContainer = sectionTag.GetCompound("biomes");
                PalettedContainer<Holder<Biome>>? biomes = null;
                if (biomesContainer is not null)
                {
                    var result = biomeCodec.Parse(NbtOps.Instance, biomesContainer);
                    biomes = result.GetOrThrow(msg => new ChunkReadException(msg));
                }
                biomes ??= containerFactory.CreateForBiomes();

                section = new LevelChunkSection(blocks, biomes);
            }
            else
            {
                section = null;
            }

            var blockLightBytes = sectionTag.GetByteArray(BlockLightTag);
            var blockLight = blockLightBytes is not null ? new DataLayer(blockLightBytes.Value) : null;
            var skyLightBytes = sectionTag.GetByteArray(SkyLightTag);
            var skyLight = skyLightBytes is not null ? new DataLayer(skyLightBytes.Value) : null;

            sectionData.Add(new SectionData(y, section, blockLight, skyLight));
        }

        var parsed = new SerializableChunkData(
            containerFactory, chunkPos, levelHeight.MinSectionY,
            lastUpdateTime, inhabitedTime, status,
            blendingData, belowZeroRetrogen, upgradeData, carvingMask,
            heightmaps, packedTicks, postProcessingSections, lightCorrect,
            sectionData, entities, blockEntities, structureData);
        //Log.Debug($"Parse 出口 result={parsed}");
        return parsed;
    }

    //CopyOf 从 ChunkAccess 提取数据生成 SerializableChunkData 对应原版 SerializableChunkData.copyOf
    //level 提供 RegistryAccess 与 DataVersionchunk 提供 Pos/Status/Sections/Heightmaps
    //factory 显式传入的容器工厂未传时回退 PalettedContainerFactory.Default
    public static SerializableChunkData CopyOf(ServerLevel level, ChunkAccess chunk, PalettedContainerFactory? factory = null)
    {
        //og.Debug($"CopyOf 入口 level={level} chunk={chunk} factory={factory}");
        factory ??= PalettedContainerFactory.Default;
        var sectionData = new List<SectionData>(chunk.SectionsCount);
        for (var i = 0; i < chunk.SectionsCount; i++)
        {
            var sectionY = chunk.MinSectionY + i;
            var section = chunk.GetSection(sectionY);
            if (section is null || section.HasOnlyAir())
                sectionData.Add(new SectionData(sectionY, null, null, null));
            else
                sectionData.Add(new SectionData(sectionY, section.Copy(), null, null));
        }

        var heightmaps = new Dictionary<Heightmap.Types, long[]>();
        foreach (var (type, data) in chunk.Heightmaps)
            heightmaps[type] = data;

        var sourcePostProcessing = chunk.PostProcessingSections;
        var postProcessingSections = new List<short>?[chunk.SectionsCount];
        for (var i = 0; i < postProcessingSections.Length && i < sourcePostProcessing.Length; i++)
            postProcessingSections[i] = sourcePostProcessing[i] is null
                ? null
                : new List<short>(sourcePostProcessing[i]!);

        var result = new SerializableChunkData(
            factory, chunk.Pos, chunk.MinSectionY,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0L, chunk.ChunkStatus,
            null, null, UpgradeData.Empty,
            chunk is ProtoChunk proto ? proto.ExistingCarvingMask?.ToArray() : null,
            heightmaps, PackTicks(chunk, level.GameTime), postProcessingSections, false,
            sectionData, new List<CompoundTag>(),
            //方块实体由 Game 层桥采集 未注入时该区块不带方块实体
            level.BlockEntityBridge?.Collect(chunk.Pos) ?? new List<CompoundTag>(),
            //结构装配结果同样由 Game 层桥打包 未注入时该区块不带 structures 段
            level.StructureDataBridge?.Pack(chunk.Pos) ?? new CompoundTag());
        Log.Debug($"CopyOf exit result={result}");
        return result;
    }

    //Read 从 SerializableChunkData 还原 ChunkAccess 对应原版 SerializableChunkData.read
    //level 提供 RegistryAccesspoiManager 用于 Poi 同步（当前 stub 跳过）
    //regionInfo 为可选的 blending 上下文未启用时传 null
    //sectionsCount 优先取 level 实现的 LevelHeightAccessor.SectionsCount 保持原 chunk 区段数
    //返回 LevelChunk 含区段与高度图还原后的实例
    public LevelChunk Read(ServerLevel level, PoiManager poiManager, object? regionInfo, ChunkPos pos)
    {
        //Log.Debug($"Read 入口 level={level} poiManager={poiManager} regionInfo={regionInfo} pos={pos}");
        //Log.Debug($"Read 入口 level={level}pos={pos}");
        var factory = ContainerFactory;
        var sectionsCount = level is LevelHeightAccessor accessor
            ? accessor.SectionsCount
            : SectionDataList.Count;
        var chunk = new LevelChunk(
            ChunkPos, MinSectionY, sectionsCount,
            factory.CreateForBlockStates, factory.CreateForBiomes,
            level);
        //雕刻标记是逐区块的增量数据 读回来才能保证重新雕刻时不会重复挖同一处
        if (CarvingMask is not null)
            chunk.SetCarvingMask(new NetCraft.Storage.Chunk.CarvingMask(CarvingMask, MinSectionY * 16));
        chunk.SetPostProcessingSections(PostProcessingSections);

        //调度刻容器整体换成读档带回的那批 延迟保持相对值 等区块登记进关卡时再按当时游戏刻展开
        chunk.SetBlockTicks(new LevelChunkTicks<NetCraft.Registry.Block>(
            UnpackTicks(PackedTicks.Blocks, ResolveBlock)));
        chunk.SetFluidTicks(new LevelChunkTicks<NetCraft.Registry.Fluid>(
            UnpackTicks(PackedTicks.Fluids, ResolveFluid)));

        foreach (var section in SectionDataList)
        {
            if (section.ChunkSection is null) continue;
            var idx = section.Y - MinSectionY;
            if (idx < 0 || idx >= chunk.SectionsCount) continue;
            chunk.SetSection(section.Y, section.ChunkSection);
        }

        foreach (var (type, data) in Heightmaps)
            chunk.Heightmaps[type] = data;

        //方块实体在区段就位之后再还原 由 Game 层桥按 id 反查类型 未注入时读档不带方块实体
        if (BlockEntities.Count > 0) level.BlockEntityBridge?.Restore(pos, BlockEntities);

        //结构装配结果还原回结构表 后续装饰与地形适配都要按区块查它
        if (!StructureData.IsEmpty) level.StructureDataBridge?.Restore(pos, StructureData);

        //Log.Debug($"Read 出口 result={chunk}");
        return chunk;
    }

    //PackTicks 把区块内两套调度刻容器打包成存档形态 对应原版 ChunkAccess.getPackedTicks
    //不打包的话中继器观察者这类靠调度刻推进的元件会在读档后停在半路
    private static PackedTicks PackTicks(ChunkAccess chunk, long gameTime)
    {
        var blocks = new List<CompoundTag>();
        foreach (var tick in chunk.BlockTicks.Pack(gameTime))
        {
            var name = BuiltInRegistries.BLOCK.GetKey(tick.Type);
            if (name is not null) blocks.Add(tick.ToCompoundTag(name.ToString()));
        }
        var fluids = new List<CompoundTag>();
        foreach (var tick in chunk.FluidTicks.Pack(gameTime))
        {
            var name = BuiltInRegistries.FLUID.GetKey(tick.Type);
            if (name is not null) fluids.Add(tick.ToCompoundTag(name.ToString()));
        }
        return new PackedTicks(blocks, fluids);
    }

    //UnpackTicks 把存档形态的刻列表还原成待展开批 查不到类型名的条目丢弃
    private static List<SavedTick<T>> UnpackTicks<T>(IEnumerable<CompoundTag> tags, Func<string, T?> resolve)
        where T : class
    {
        var result = new List<SavedTick<T>>();
        foreach (var tag in tags)
        {
            var tick = SavedTick<T>.FromCompoundTag(tag, resolve);
            if (tick is not null) result.Add(tick);
        }
        return result;
    }

    //ResolveBlock 按注册名找回方块类型
    private static NetCraft.Registry.Block? ResolveBlock(string name)
        => Identifier.TryParse(name) is { } id ? BuiltInRegistries.BLOCK.GetValue(id) : null;

    //ResolveFluid 按注册名找回流体类型
    private static NetCraft.Registry.Fluid? ResolveFluid(string name)
        => Identifier.TryParse(name) is { } id ? BuiltInRegistries.FLUID.GetValue(id) : null;
}