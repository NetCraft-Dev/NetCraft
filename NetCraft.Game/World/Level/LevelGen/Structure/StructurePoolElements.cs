using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;
using RegistryProcessorList = NetCraft.Registry.StructureProcessorList;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//EmptyPoolElement empty element, maps to vanilla EmptyPoolElement
//Places nothing; reaching it ends the jigsaw, it has no bounding box and does not count toward the max size
public sealed class EmptyPoolElement : StructurePoolElement
{
    public static readonly EmptyPoolElement Instance = new();

    //MapCodec unit codec that ignores all fields, maps to vanilla MapCodec.unit
    public static readonly MapCodec<EmptyPoolElement> MapCodec = new StructureUnitMapCodec<EmptyPoolElement>(() => Instance);

    private EmptyPoolElement()
        : base(StructureTemplatePool.Projection.TerrainMatching) { }

    public override Vec3i GetSize(StructureTemplateManager manager, Rotation rotation) => Vec3i.Zero;

    public override List<StructureTemplate.JigsawBlockInfo> GetShuffledJigsawBlocks(StructureTemplateManager manager,
        BlockPos position, Rotation rotation, RandomSource random) => new();

    //GetBoundingBox an empty element has no bounding box; vanilla throws right here
    public override BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation)
        => throw new InvalidOperationException("empty element has no bounding box, filter it out before calling");

    public override bool Place(StructureTemplateManager manager, WorldGenRegion level, StructureManager structureManager,
        ChunkGenerator generator, BlockPos position, BlockPos referencePos, Rotation rotation, BoundingBoxInt chunkBB,
        RandomSource random, LiquidSettings liquidSettings, bool keepJigsaws) => true;

    public override StructurePoolElementType GetType() => EmptyPoolElementType.Instance;

    public override string ToString() => "Empty";
}

//SinglePoolElement single-template element, maps to vanilla SinglePoolElement
//Places one structure template; the processor chain = built-in processors + processors declared by the pool element + processors built into the projection
public class SinglePoolElement : StructurePoolElement
{
    public static readonly MapCodec<SinglePoolElement> MapCodec =
        RecordCodecBuilder.Of4<SinglePoolElement, Identifier, Holder<RegistryProcessorList>,
            StructureTemplatePool.Projection, Optional<LiquidSettings>>(
            StructurePoolCodecs.TemplateLocation.FieldOf("location")
                .ForGetter<SinglePoolElement, Identifier>(e => e.TemplateLocation),
            StructurePoolCodecs.ProcessorListRef.FieldOf("processors")
                .ForGetter<SinglePoolElement, Holder<RegistryProcessorList>>(e => e.Processors),
            StructurePoolCodecs.ProjectionCodec.FieldOf("projection")
                .ForGetter<SinglePoolElement, StructureTemplatePool.Projection>(e => e.Projection),
            StructurePoolCodecs.LiquidSettingsCodec.OptionalFieldOf("override_liquid_settings")
                .ForGetter<SinglePoolElement, Optional<LiquidSettings>>(e => e.OverrideLiquidSettings),
            (location, processors, projection, overrideLiquidSettings)
                => new SinglePoolElement(location, processors, projection, overrideLiquidSettings));

    public SinglePoolElement(Identifier templateLocation, Holder<RegistryProcessorList> processors,
        StructureTemplatePool.Projection projection, Optional<LiquidSettings> overrideLiquidSettings)
        : base(projection)
    {
        TemplateLocation = templateLocation;
        Processors = processors;
        OverrideLiquidSettings = overrideLiquidSettings;
    }

    //TemplateLocation the referenced structure template; vanilla can also hold a runtime template here, but the JSON only has the registry name
    public Identifier TemplateLocation { get; }

    public Holder<RegistryProcessorList> Processors { get; }

    //OverrideLiquidSettings overrides the liquid handling passed into the pool element; when absent the outer value is used
    public Optional<LiquidSettings> OverrideLiquidSettings { get; }

    public override Vec3i GetSize(StructureTemplateManager manager, Rotation rotation)
        => GetTemplate(manager)?.GetSize(rotation) ?? Vec3i.Zero;

    public override List<StructureTemplate.JigsawBlockInfo> GetShuffledJigsawBlocks(StructureTemplateManager manager,
        BlockPos position, Rotation rotation, RandomSource random)
    {
        var template = GetTemplate(manager);
        if (template is null) return new List<StructureTemplate.JigsawBlockInfo>();
        var jigsaws = template.GetJigsaws(position, rotation);
        RandomCollections.Shuffle(jigsaws, random);
        //Vanilla List.sort is stable; a stable chained ordering matches it here, putting higher selection priority first
        //Sorted in place by insertion sort instead of OrderByDescending plus ToList, which built an ordered iterator, a
        //comparison delegate and a second list on every call, and this runs once per placement attempt
        //Insertion sort is stable just like the vanilla sort, so entries with equal priority keep the shuffled order they
        //came out in and the result is bit for bit the same; a jigsaw list is only a handful of entries anyway
        for (var i = 1; i < jigsaws.Count; i++)
        {
            var pending = jigsaws[i];
            var j = i - 1;
            while (j >= 0 && jigsaws[j].SelectionPriority < pending.SelectionPriority)
            {
                jigsaws[j + 1] = jigsaws[j];
                j--;
            }
            jigsaws[j + 1] = pending;
        }
        return jigsaws;
    }

    public override BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation)
    {
        var template = GetTemplate(manager);
        //A degenerate bounding box when the template is missing; in vanilla the template always exists on this path
        if (template is null) return new BoundingBoxInt(position.X, position.Y, position.Z, position.X, position.Y, position.Z);
        return template.GetBoundingBox(new StructurePlaceSettings().SetRotation(rotation), position);
    }

    public override bool Place(StructureTemplateManager manager, WorldGenRegion level, StructureManager structureManager,
        ChunkGenerator generator, BlockPos position, BlockPos referencePos, Rotation rotation, BoundingBoxInt chunkBB,
        RandomSource random, LiquidSettings liquidSettings, bool keepJigsaws)
    {
        var template = GetTemplate(manager);
        if (template is null) return false;
        var settings = GetSettings(rotation, chunkBB, liquidSettings, keepJigsaws);
        if (!template.PlaceInWorld(level, position, referencePos, settings, random)) return false;
        var dataMarkers = StructureTemplate.ProcessBlockInfos(level, position, referencePos, settings,
            GetDataMarkers(manager, position, rotation, false));
        foreach (var dataMarker in dataMarkers)
            HandleDataMarker(level, dataMarker, position, rotation, random, chunkBB);
        return true;
    }

    //GetDataMarkers returns the data-mode structure blocks in the template; when absolute is true the in-template coords are kept
    public List<StructureBlockInfo> GetDataMarkers(StructureTemplateManager manager, BlockPos position,
        Rotation rotation, bool absolute)
    {
        var result = new List<StructureBlockInfo>();
        var template = GetTemplate(manager);
        var structureBlock = ProcessorBlockHelper.BlockOf("structure_block");
        if (template is null || structureBlock is null) return result;
        var blocks = template.FilterBlocks(position, new StructurePlaceSettings().SetRotation(rotation),
            structureBlock, absolute);
        foreach (var info in blocks)
        {
            var mode = info.Nbt?.GetString("mode")?.Value;
            //Entries with a missing or invalid mode throw in vanilla; here they are treated as not data markers
            if (!Enum.TryParse<StructureMode>(mode, true, out var parsed) || parsed != StructureMode.data) continue;
            result.Add(info);
        }
        return result;
    }

    //GetSettings assembles the placement settings, maps to vanilla getSettings
    protected virtual StructurePlaceSettings GetSettings(Rotation rotation, BoundingBoxInt chunkBB,
        LiquidSettings liquidSettings, bool keepJigsaws)
    {
        var settings = new StructurePlaceSettings();
        settings.SetBoundingBox(chunkBB);
        settings.SetRotation(rotation);
        settings.SetKnownShape(true);
        settings.SetIgnoreEntities(false);
        settings.AddProcessor(BlockIgnoreProcessor.StructureBlock);
        settings.SetFinalizeEntities(true);
        settings.SetLiquidSettings(OverrideLiquidSettings.OrElse(liquidSettings));
        if (!keepJigsaws) settings.AddProcessor(JigsawReplacementProcessor.Instance);
        if (Processors.Value is StructureProcessorList processorList)
        {
            foreach (var processor in processorList.List()) settings.AddProcessor(processor);
        }
        foreach (var processor in PoolProjections.GetProcessors(Projection)) settings.AddProcessor(processor);
        return settings;
    }

    //GetTemplate fetches the template by registry name, returns null when the manager cannot load it
    private StructureTemplate? GetTemplate(StructureTemplateManager manager) => manager.GetOrLoad(TemplateLocation);

    public override StructurePoolElementType GetType() => SinglePoolElementType.Instance;

    public override string ToString() => $"Single[{TemplateLocation}]";
}

//LegacySinglePoolElement legacy single-template element, maps to vanilla LegacySinglePoolElement
//Differs from the single-template element only in the processor chain: both structure blocks and air are ignored
public sealed class LegacySinglePoolElement : SinglePoolElement
{
    public static new readonly MapCodec<LegacySinglePoolElement> MapCodec =
        RecordCodecBuilder.Of4<LegacySinglePoolElement, Identifier, Holder<RegistryProcessorList>,
            StructureTemplatePool.Projection, Optional<LiquidSettings>>(
            StructurePoolCodecs.TemplateLocation.FieldOf("location")
                .ForGetter<LegacySinglePoolElement, Identifier>(e => e.TemplateLocation),
            StructurePoolCodecs.ProcessorListRef.FieldOf("processors")
                .ForGetter<LegacySinglePoolElement, Holder<RegistryProcessorList>>(e => e.Processors),
            StructurePoolCodecs.ProjectionCodec.FieldOf("projection")
                .ForGetter<LegacySinglePoolElement, StructureTemplatePool.Projection>(e => e.Projection),
            StructurePoolCodecs.LiquidSettingsCodec.OptionalFieldOf("override_liquid_settings")
                .ForGetter<LegacySinglePoolElement, Optional<LiquidSettings>>(e => e.OverrideLiquidSettings),
            (location, processors, projection, overrideLiquidSettings)
                => new LegacySinglePoolElement(location, processors, projection, overrideLiquidSettings));

    public LegacySinglePoolElement(Identifier templateLocation, Holder<RegistryProcessorList> processors,
        StructureTemplatePool.Projection projection, Optional<LiquidSettings> overrideLiquidSettings)
        : base(templateLocation, processors, projection, overrideLiquidSettings) { }

    protected override StructurePlaceSettings GetSettings(Rotation rotation, BoundingBoxInt chunkBB,
        LiquidSettings liquidSettings, bool keepJigsaws)
    {
        var settings = base.GetSettings(rotation, chunkBB, liquidSettings, keepJigsaws);
        settings.PopProcessor(BlockIgnoreProcessor.StructureBlock);
        settings.AddProcessor(BlockIgnoreProcessor.StructureAndAir);
        return settings;
    }

    public override StructurePoolElementType GetType() => LegacySinglePoolElementType.Instance;

    public override string ToString() => $"LegacySingle[{TemplateLocation}]";
}

//ListPoolElement template list element, maps to vanilla ListPoolElement
//Size takes the max across elements, the bounding box takes the union, and placement must succeed on all of them
public sealed class ListPoolElement : StructurePoolElement
{
    public static readonly MapCodec<ListPoolElement> MapCodec =
        RecordCodecBuilder.Of2<ListPoolElement, IReadOnlyList<StructurePoolElement>, StructureTemplatePool.Projection>(
            StructurePoolElement.Codec.ListOf().FieldOf("elements")
                .ForGetter<ListPoolElement, IReadOnlyList<StructurePoolElement>>(e => e.Elements),
            StructurePoolCodecs.ProjectionCodec.FieldOf("projection")
                .ForGetter<ListPoolElement, StructureTemplatePool.Projection>(e => e.Projection),
            (elements, projection) => new ListPoolElement(elements, projection));

    private readonly List<StructurePoolElement> _elements;

    public ListPoolElement(IReadOnlyList<StructurePoolElement> elements, StructureTemplatePool.Projection projection)
        : base(projection)
    {
        if (elements.Count == 0) throw new ArgumentException("pool element list must not be empty");
        _elements = new List<StructurePoolElement>(elements);
        SetProjectionOnEachElement(projection);
    }

    public IReadOnlyList<StructurePoolElement> Elements => _elements;

    public override Vec3i GetSize(StructureTemplateManager manager, Rotation rotation)
    {
        var sizeX = 0;
        var sizeY = 0;
        var sizeZ = 0;
        foreach (var element in _elements)
        {
            var size = element.GetSize(manager, rotation);
            sizeX = Math.Max(sizeX, size.X);
            sizeY = Math.Max(sizeY, size.Y);
            sizeZ = Math.Max(sizeZ, size.Z);
        }
        return new Vec3i(sizeX, sizeY, sizeZ);
    }

    //GetShuffledJigsawBlocks a list element takes the jigsaws of only the first element, matching vanilla
    public override List<StructureTemplate.JigsawBlockInfo> GetShuffledJigsawBlocks(StructureTemplateManager manager,
        BlockPos position, Rotation rotation, RandomSource random)
        => _elements[0].GetShuffledJigsawBlocks(manager, position, rotation, random);

    public override BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation)
    {
        BoundingBoxInt? box = null;
        foreach (var element in _elements)
        {
            if (ReferenceEquals(element, EmptyPoolElement.Instance)) continue;
            var elementBox = element.GetBoundingBox(manager, position, rotation);
            box = box is null ? elementBox : box.Encapsulate(elementBox);
        }
        return box ?? throw new InvalidOperationException("cannot compute the list element bounding box");
    }

    public override bool Place(StructureTemplateManager manager, WorldGenRegion level, StructureManager structureManager,
        ChunkGenerator generator, BlockPos position, BlockPos referencePos, Rotation rotation, BoundingBoxInt chunkBB,
        RandomSource random, LiquidSettings liquidSettings, bool keepJigsaws)
    {
        foreach (var element in _elements)
        {
            if (!element.Place(manager, level, structureManager, generator, position, referencePos, rotation, chunkBB,
                    random, liquidSettings, keepJigsaws)) return false;
        }
        return true;
    }

    public override StructurePoolElementType GetType() => ListPoolElementType.Instance;

    public override StructurePoolElement SetProjection(StructureTemplatePool.Projection projection)
    {
        base.SetProjection(projection);
        SetProjectionOnEachElement(projection);
        return this;
    }

    public override string ToString() => $"List[{string.Join(", ", _elements)}]";

    //SetProjectionOnEachElement the projection of a list element always equals the outer projection
    private void SetProjectionOnEachElement(StructureTemplatePool.Projection projection)
        => _elements.ForEach(element => element.SetProjection(projection));
}

//FeaturePoolElement feature element, maps to vanilla FeaturePoolElement
//Places a placed feature instead of a template; carries a downward-facing jigsaw block for connections
public sealed class FeaturePoolElement : StructurePoolElement
{
    public static readonly MapCodec<FeaturePoolElement> MapCodec =
        RecordCodecBuilder.Of2<FeaturePoolElement, Holder<NetCraft.Registry.PlacedFeature>, StructureTemplatePool.Projection>(
            StructurePoolCodecs.PlacedFeatureRef.FieldOf("feature")
                .ForGetter<FeaturePoolElement, Holder<NetCraft.Registry.PlacedFeature>>(e => e.Feature),
            StructurePoolCodecs.ProjectionCodec.FieldOf("projection")
                .ForGetter<FeaturePoolElement, StructureTemplatePool.Projection>(e => e.Projection),
            (feature, projection) => new FeaturePoolElement(feature, projection));

    private const string DefaultFinalState = "minecraft:air";

    //Default data of the virtual jigsaw block, maps to vanilla defaultJigsawNBT
    private readonly CompoundTag _defaultJigsawNbt;

    public FeaturePoolElement(Holder<NetCraft.Registry.PlacedFeature> feature, StructureTemplatePool.Projection projection)
        : base(projection)
    {
        Feature = feature;
        _defaultJigsawNbt = FillDefaultJigsawNbt();
    }

    public Holder<NetCraft.Registry.PlacedFeature> Feature { get; }

    public override Vec3i GetSize(StructureTemplateManager manager, Rotation rotation) => Vec3i.Zero;

    public override List<StructureTemplate.JigsawBlockInfo> GetShuffledJigsawBlocks(StructureTemplateManager manager,
        BlockPos position, Rotation rotation, RandomSource random)
    {
        var jigsawBlock = ProcessorBlockHelper.BlockOf("jigsaw");
        //Without the jigsaw block registered, not even the marker can be built; in vanilla the block always exists here
        if (jigsawBlock is null) return new List<StructureTemplate.JigsawBlockInfo>();
        var state = StructurePlacementState(jigsawBlock.DefaultBlockState);
        return new List<StructureTemplate.JigsawBlockInfo>
        {
            StructureTemplate.JigsawBlockInfo.Of(new StructureBlockInfo(position, state, _defaultJigsawNbt)),
        };
    }

    public override BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation)
    {
        var size = GetSize(manager, rotation);
        return new BoundingBoxInt(position.X, position.Y, position.Z,
            position.X + size.X, position.Y + size.Y, position.Z + size.Z);
    }

    public override bool Place(StructureTemplateManager manager, WorldGenRegion level, StructureManager structureManager,
        ChunkGenerator generator, BlockPos position, BlockPos referencePos, Rotation rotation, BoundingBoxInt chunkBB,
        RandomSource random, LiquidSettings liquidSettings, bool keepJigsaws)
    {
        //Feature placement needs the full modifier chain and generator; without a generator it conservatively reports not placed
        if (generator is null) return false;
        return Feature.Value is Placement.PlacedFeature placed
            && placed.Place(level, generator, random, position);
    }

    public override StructurePoolElementType GetType() => FeaturePoolElementType.Instance;

    public override string ToString() => $"Feature[{Feature.RegisteredName}]";

    //FillDefaultJigsawNbt builds the default jigsaw data, maps to vanilla fillDefaultJigsawNBT
    private static CompoundTag FillDefaultJigsawNbt()
    {
        var tag = new CompoundTag();
        tag.PutString("name", Identifier.WithDefaultNamespace("bottom").ToString());
        tag.PutString("final_state", DefaultFinalState);
        tag.PutString("pool", Identifier.WithDefaultNamespace("empty").ToString());
        tag.PutString("target", Identifier.WithDefaultNamespace("empty").ToString());
        tag.PutString("joint", JointTypes.Name(JointType.Rollable));
        return tag;
    }

    //StructurePlacementState changes the default orientation to down/south, maps to vanilla FrontAndTop.DOWN_SOUTH
    private static BlockState StructurePlacementState(BlockState state)
    {
        foreach (var property in state.GetProperties())
        {
            if (property.Name != "orientation") continue;
            return StructureBlockTransforms.SetIfAllowed(state, property, FrontAndTop.down_south);
        }
        return state;
    }
}
