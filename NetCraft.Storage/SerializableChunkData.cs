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

//SerializableChunkData, maps to vanilla net.minecraft.world.level.chunk.storage.SerializableChunkData
//Intermediate representation of chunk data serialization; write produces a CompoundTag and parse deserializes from it
//copyOf/read depend on ServerLevel and other game content that is not implemented and throw NotSupportedException
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

    //SectionData, maps to vanilla SerializableChunkData.SectionData
    //Packs the section Y with its LevelChunkSection and light data
    public sealed record SectionData(int Y, LevelChunkSection? ChunkSection, DataLayer? BlockLight, DataLayer? SkyLight);

    //write, maps to vanilla SerializableChunkData.write
    //Serializes all fields into a CompoundTag for writing to the MCA file
    public CompoundTag Write()
    {
        //Log.Debug($"Write entry");
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
        //Log.Debug($"Write exit pos={ChunkPos} sections={SectionDataList.Count}");
        return tag;
    }

    //saveTicks, maps to vanilla saveTicks
    //Stubbed as a ListTag of CompoundTag, keeping the raw tick data
    private static void SaveTicks(CompoundTag tag, PackedTicks ticks)
    {
        var blockTicks = new ListTag();
        foreach (var t in ticks.Blocks) blockTicks.Add(t);
        tag.Put(BlockTicksTag, blockTicks);

        var fluidTicks = new ListTag();
        foreach (var t in ticks.Fluids) fluidTicks.Add(t);
        tag.Put(FluidTicksTag, fluidTicks);
    }

    //packOffsets, maps to vanilla packOffsets
    //Each ShortList becomes a ListTag of ShortTag
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

    //parse, maps to vanilla SerializableChunkData.parse
    //Deserialize chunk data from a CompoundTag; null means there is no Status field
    public static SerializableChunkData? Parse(LevelHeightAccessor levelHeight, PalettedContainerFactory containerFactory, CompoundTag chunkData)
    {
        //Log.Debug($"Parse entry levelHeight={levelHeight} containerFactory={containerFactory} chunkData={chunkData}");
        //Log.Debug($"Parse entry levelHeight={levelHeight} containerFactory={containerFactory}");
        if (string.IsNullOrEmpty(chunkData.GetStringValue(StatusTag)))
        {
            //Log.Debug($"Parse exit result=null");
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
            //In anvil, Y is a signed byte and negative sections are written two's-complement; it must be interpreted as sbyte or it is deemed out of range and dropped
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
        //Log.Debug($"Parse exit result={parsed}");
        return parsed;
    }

    //CopyOf extracts data from a ChunkAccess into a SerializableChunkData, maps to vanilla SerializableChunkData.copyOf
    //level provides RegistryAccess and DataVersion; chunk provides Pos/Status/Sections/Heightmaps
    //factory, the explicitly passed container factory; falls back to PalettedContainerFactory.Default when not passed
    public static SerializableChunkData CopyOf(ServerLevel level, ChunkAccess chunk, PalettedContainerFactory? factory = null)
    {
        //og.Debug($"CopyOf entry level={level} chunk={chunk} factory={factory}");
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
            //Block entities are collected by the Game-layer bridge; without injection the chunk carries no block entities
            level.BlockEntityBridge?.Collect(chunk.Pos) ?? new List<CompoundTag>(),
            //Structure placements are likewise packed by the Game-layer bridge; without injection the chunk carries no structures section
            level.StructureDataBridge?.Pack(chunk.Pos) ?? new CompoundTag());
        Log.Debug($"CopyOf exit result={result}");
        return result;
    }

    //Read restores a ChunkAccess from a SerializableChunkData, maps to vanilla SerializableChunkData.read
    //level provides RegistryAccess; poiManager is for Poi sync (currently a skipped stub)
    //regionInfo is the optional blending context; pass null when not enabled
    //sectionsCount prefers the LevelHeightAccessor.SectionsCount implemented by level, keeping the original chunk's section count
    //Returns a LevelChunk with sections and heightmaps restored
    public LevelChunk Read(ServerLevel level, PoiManager poiManager, object? regionInfo, ChunkPos pos)
    {
        //Log.Debug($"Read entry level={level} poiManager={poiManager} regionInfo={regionInfo} pos={pos}");
        //Log.Debug($"Read entry level={level}pos={pos}");
        var factory = ContainerFactory;
        var sectionsCount = level is LevelHeightAccessor accessor
            ? accessor.SectionsCount
            : SectionDataList.Count;
        var chunk = new LevelChunk(
            ChunkPos, MinSectionY, sectionsCount,
            factory.CreateForBlockStates, factory.CreateForBiomes,
            level);
        //The carving mask is per-chunk incremental data; reading it back prevents carving the same spot twice
        if (CarvingMask is not null)
            chunk.SetCarvingMask(new NetCraft.Storage.Chunk.CarvingMask(CarvingMask, MinSectionY * 16));
        chunk.SetPostProcessingSections(PostProcessingSections);

        //The scheduled tick container is replaced wholesale with the batch read from disk; delays stay relative and are expanded against the then-current game tick when the chunk registers into the level
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

        //Block entities are restored after sections are in place; the Game-layer bridge resolves types by id, and without injection a load carries no block entities
        if (BlockEntities.Count > 0) level.BlockEntityBridge?.Restore(pos, BlockEntities);

        //Structure placements are restored into the structure table; later decoration and terrain fitting query it per chunk
        if (!StructureData.IsEmpty) level.StructureDataBridge?.Restore(pos, StructureData);

        //Log.Debug($"Read exit result={chunk}");
        return chunk;
    }

    //PackTicks packs the chunk's two scheduled tick containers into save form, maps to vanilla ChunkAccess.getPackedTicks
    //Without packing, components driven by scheduled ticks like repeaters and observers would stall partway after a load
    private static PackedTicks PackTicks(ChunkAccess chunk, long gameTime)
    {
        var blocks = new List<CompoundTag>();
        foreach (var tick in chunk.BlockTicks.Pack(gameTime))
        {
            var name = BuiltInRegistries.BLOCK.GetKey(tick.Type);
            if (name is { } id) blocks.Add(tick.ToCompoundTag(id.ToString()));
        }
        var fluids = new List<CompoundTag>();
        foreach (var tick in chunk.FluidTicks.Pack(gameTime))
        {
            var name = BuiltInRegistries.FLUID.GetKey(tick.Type);
            if (name is { } id) fluids.Add(tick.ToCompoundTag(id.ToString()));
        }
        return new PackedTicks(blocks, fluids);
    }

    //UnpackTicks restores the saved tick list into a pending batch, dropping entries whose type name cannot be resolved
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

    //ResolveBlock resolves the block type by registry name
    private static NetCraft.Registry.Block? ResolveBlock(string name)
        => Identifier.TryParse(name) is { } id ? BuiltInRegistries.BLOCK.GetValue(id) : null;

    //ResolveFluid resolves the fluid type by registry name
    private static NetCraft.Registry.Fluid? ResolveFluid(string name)
        => Identifier.TryParse(name) is { } id ? BuiltInRegistries.FLUID.GetValue(id) : null;
}
