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

//EmptyPoolElement 空元素 对应原版 EmptyPoolElement
//什么都不放 拼图走到它就算结束 没有包围盒也不参与最大尺寸计算
public sealed class EmptyPoolElement : StructurePoolElement
{
    public static readonly EmptyPoolElement Instance = new();

    //MapCodec 单位 codec 忽略一切字段 对应原版 MapCodec.unit
    public static readonly MapCodec<EmptyPoolElement> MapCodec = new StructureUnitMapCodec<EmptyPoolElement>(() => Instance);

    private EmptyPoolElement()
        : base(StructureTemplatePool.Projection.TerrainMatching) { }

    public override Vec3i GetSize(StructureTemplateManager manager, Rotation rotation) => Vec3i.Zero;

    public override List<StructureTemplate.JigsawBlockInfo> GetShuffledJigsawBlocks(StructureTemplateManager manager,
        BlockPos position, Rotation rotation, RandomSource random) => new();

    //GetBoundingBox 空元素没有包围盒 原版这里直接抛异常
    public override BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation)
        => throw new InvalidOperationException("空元素没有包围盒 调用前应先过滤");

    public override bool Place(StructureTemplateManager manager, WorldGenRegion level, StructureManager structureManager,
        ChunkGenerator generator, BlockPos position, BlockPos referencePos, Rotation rotation, BoundingBoxInt chunkBB,
        RandomSource random, LiquidSettings liquidSettings, bool keepJigsaws) => true;

    public override StructurePoolElementType GetType() => EmptyPoolElementType.Instance;

    public override string ToString() => "Empty";
}

//SinglePoolElement 单模板元素 对应原版 SinglePoolElement
//放置一个结构模板 处理器链 = 自带处理器 + 池元素声明的处理器 + 投影自带的处理器
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

    //TemplateLocation 引用的结构模板 原版这里还能直接持运行时模板 JSON 里只有注册名
    public Identifier TemplateLocation { get; }

    public Holder<RegistryProcessorList> Processors { get; }

    //OverrideLiquidSettings 覆盖池元素入参的液体处理方式 未声明时用外层传的
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
        //原版 List.sort 是稳定排序 这里用稳定排序链式表达式对齐 选择优先级高的排前面
        return jigsaws.OrderByDescending(jigsaw => jigsaw.SelectionPriority).ToList();
    }

    public override BoundingBoxInt GetBoundingBox(StructureTemplateManager manager, BlockPos position, Rotation rotation)
    {
        var template = GetTemplate(manager);
        //模板缺失时给退化包围盒 原版这条路径上模板必定存在
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

    //GetDataMarkers 取模板里 data 模式的结构方块 absolute 为真时保留模板内坐标
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
            //缺 mode 或 mode 非法的条目原版会抛异常 这里按不是数据标记处理
            if (!Enum.TryParse<StructureMode>(mode, true, out var parsed) || parsed != StructureMode.data) continue;
            result.Add(info);
        }
        return result;
    }

    //GetSettings 组装放置设置 对应原版 getSettings
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

    //GetTemplate 按注册名取模板 管理器读不到返回 null
    private StructureTemplate? GetTemplate(StructureTemplateManager manager) => manager.GetOrLoad(TemplateLocation);

    public override StructurePoolElementType GetType() => SinglePoolElementType.Instance;

    public override string ToString() => $"Single[{TemplateLocation}]";
}

//LegacySinglePoolElement 旧版单模板元素 对应原版 LegacySinglePoolElement
//与单模板元素的差别只在处理器链 结构方块与空气都忽略
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

//ListPoolElement 模板列表元素 对应原版 ListPoolElement
//尺寸取各元素最大值 包围盒取并集 放置要全部成功
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
        if (elements.Count == 0) throw new ArgumentException("池元素列表不能为空");
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

    //GetShuffledJigsawBlocks 列表元素只取第一个元素的拼图 对应原版行为
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
        return box ?? throw new InvalidOperationException("列表元素的包围盒算不出来");
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

    //SetProjectionOnEachElement 列表元素的投影恒等于外层投影
    private void SetProjectionOnEachElement(StructureTemplatePool.Projection projection)
        => _elements.ForEach(element => element.SetProjection(projection));
}

//FeaturePoolElement 特征元素 对应原版 FeaturePoolElement
//不放置模板而是放置一个已放置特征 自带一个指向下方的拼图方块供连接
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

    //虚拟拼图方块的默认数据 对应原版 defaultJigsawNBT
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
        //拼图方块没注册时连标记都造不出来 原版这里恒有方块
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
        //特征放置要走完整的修饰器链与生成器 缺生成器时保守返回未放置
        if (generator is null) return false;
        return Feature.Value is Placement.PlacedFeature placed
            && placed.Place(level, generator, random, position);
    }

    public override StructurePoolElementType GetType() => FeaturePoolElementType.Instance;

    public override string ToString() => $"Feature[{Feature.RegisteredName}]";

    //FillDefaultJigsawNbt 拼一份默认拼图数据 对应原版 fillDefaultJigsawNBT
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

    //StructurePlacementState 把默认状态的朝向改成朝下朝南 对应原版 FrontAndTop.DOWN_SOUTH
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
