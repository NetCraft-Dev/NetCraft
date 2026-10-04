using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;
using DimensionTypeConstants = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawStructure 拼图结构 对应原版 net.minecraft.world.level.levelgen.structure.structures.JigsawStructure
//结构本体只负责把起始池与各类参数交给 JigsawPlacement 装配 真正的连接算法在那边
public sealed class JigsawStructure : Structure
{
    //DefaultDimensionPadding 默认不留白 对应原版 DEFAULT_DIMENSION_PADDING
    public static readonly DimensionPadding DefaultDimensionPadding = DimensionPadding.Zero;

    //DefaultLiquidSettings 默认按含水方块铺液体 对应原版 DEFAULT_LIQUID_SETTINGS
    public const LiquidSettings DefaultLiquidSettings = LiquidSettings.ApplyWaterlogging;

    //MaxTotalStructureRange 水平方向连同地形适配的总范围上限 对应原版 MAX_TOTAL_STRUCTURE_RANGE
    public const int MaxTotalStructureRange = 128;

    //MinDepth/MaxDepth 深度允许区间 对应原版 MIN_DEPTH / MAX_DEPTH
    public const int MinDepth = 0;
    public const int MaxDepth = 20;

    //TypeId 结构类型注册名 minecraft:jigsaw
    public static readonly Identifier TypeId = Identifier.WithDefaultNamespace("jigsaw");

    //ElementCodec 结构注册表元素 codec 解析含生成设置的整份 json
    public static readonly Codec<NetCraft.Registry.Structure> ElementCodec = new JigsawStructureCodec();

    //JigsawType 结构类型 登记进 STRUCTURE_TYPE 供 STRUCTURE 装载时按 type 派发
    public static readonly StructureType JigsawType = RegisterType();

    private readonly Holder<NetCraft.Registry.StructureTemplatePool> _startPool;
    private readonly Identifier? _startJigsawName;
    private readonly int _maxDepth;
    private readonly HeightProvider _startHeight;
    private readonly bool _useExpansionHack;
    private readonly NetCraft.Registry.Heightmap.Types? _projectStartToHeightmap;
    private readonly MaxDistance _maxDistanceFromCenter;
    private readonly IReadOnlyList<PoolAliasBinding> _poolAliases;
    private readonly DimensionPadding _dimensionPadding;
    private readonly LiquidSettings _liquidSettings;

    public JigsawStructure(StructureGenerationSettings settings,
        Holder<NetCraft.Registry.StructureTemplatePool> startPool, Identifier? startJigsawName, int maxDepth,
        HeightProvider startHeight, bool useExpansionHack,
        NetCraft.Registry.Heightmap.Types? projectStartToHeightmap, MaxDistance maxDistanceFromCenter,
        IReadOnlyList<PoolAliasBinding> poolAliases, DimensionPadding dimensionPadding, LiquidSettings liquidSettings)
        : base(settings)
    {
        _startPool = startPool;
        _startJigsawName = startJigsawName;
        _maxDepth = maxDepth;
        _startHeight = startHeight;
        _useExpansionHack = useExpansionHack;
        _projectStartToHeightmap = projectStartToHeightmap;
        _maxDistanceFromCenter = maxDistanceFromCenter;
        _poolAliases = poolAliases;
        _dimensionPadding = dimensionPadding;
        _liquidSettings = liquidSettings;
    }

    //Id 结构注册名 项目还没做结构注册表的数据驱动装载 先按类型名兜底
    public override Identifier Id => TypeId;

    public override StructureType Type => JigsawType;

    //TemplateManager 结构模板管理器 生成期由世界装配注入 没注入时无法读模板也就不生成
    public StructureTemplateManager? TemplateManager { get; set; }

    public Holder<NetCraft.Registry.StructureTemplatePool> StartPool => _startPool;

    public int Size => _maxDepth;

    public bool UseExpansionHack => _useExpansionHack;

    public MaxDistance MaxDistanceFromCenter => _maxDistanceFromCenter;

    public IReadOnlyList<PoolAliasBinding> PoolAliases => _poolAliases;

    //Padding 维度边界留白 属性名避开与类型名同名
    public DimensionPadding Padding => _dimensionPadding;

    public LiquidSettings LiquidSetting => _liquidSettings;

    public NetCraft.Registry.Heightmap.Types? ProjectStartToHeightmap => _projectStartToHeightmap;

    public Identifier? StartJigsawName => _startJigsawName;

    public HeightProvider StartHeight => _startHeight;

    //FindGenerationPoint 取起始高度算出结构原点再交给拼图装配 对应原版 findGenerationPoint
    public override GenerationStub? FindGenerationPoint(GenerationContext context)
    {
        var manager = TemplateManager;
        if (manager is null) return null;
        var chunkPos = context.ChunkPos;
        var height = _startHeight.Sample(context.Random,
            new WorldGenerationContext(context.ChunkGenerator, context.HeightAccessor));
        var startPos = new BlockPos(chunkPos.MinBlockX, height, chunkPos.MinBlockZ);
        return JigsawPlacement.AddPieces(context, manager, _startPool, _startJigsawName, _maxDepth, startPos,
            _useExpansionHack, _projectStartToHeightmap, _maxDistanceFromCenter,
            PoolAliasLookup.Create(_poolAliases, startPos, context.Seed), _dimensionPadding, _liquidSettings);
    }

    //RegisterType 把 minecraft:jigsaw 登记进 STRUCTURE_TYPE 已登记就复用 静态初始化幂等
    private static StructureType RegisterType()
    {
        if (BuiltInRegistries.STRUCTURE_TYPE.GetValue(TypeId) is StructureType existing) return existing;
        return (StructureType)Registry<NetCraft.Registry.StructureType<object>>.Register(
            BuiltInRegistries.STRUCTURE_TYPE, TypeId, new StructureType(TypeId, ElementCodec));
    }

    //MaxDistance 结构离原点的最大水平与竖直距离 对应原版 JigsawStructure.MaxDistance
    public sealed record MaxDistance(int Horizontal, int Vertical)
    {
        //Codec 裸整数表示水平竖直同值 对象形态分别指定 对应原版 CODEC
        public static readonly Codec<MaxDistance> Codec = new MaxDistanceCodec();

        public MaxDistance(int value)
            : this(value, value) { }

        public override string ToString() => $"MaxDistance[{Horizontal},{Vertical}]";
    }
}

//MaxDistanceCodec 最大距离编解码 裸整数或 horizontal/vertical 对象二选一
internal sealed class MaxDistanceCodec : ScalarCodec<JigsawStructure.MaxDistance>
{
    //HorizontalMin/HorizontalMax 水平距离区间 对应原版 Codec.intRange(1, 128)
    private const int HorizontalMin = 1;
    private const int HorizontalMax = 128;

    public override DataResult<JigsawStructure.MaxDistance> Parse<U>(DynamicOps<U> ops, U input)
    {
        var number = ops.GetNumberValue(input);
        if (number.Result().IsPresent)
        {
            var value = (int)number.GetOrThrow();
            return value is < HorizontalMin or > HorizontalMax
                ? DataResult<JigsawStructure.MaxDistance>.Error(() => $"水平距离越界 必须在 {HorizontalMin}..{HorizontalMax} 之间")
                : DataResult<JigsawStructure.MaxDistance>.Success(new JigsawStructure.MaxDistance(value));
        }

        return ops.GetMap(input).FlatMap(map => ParseRecord(ops, map));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, JigsawStructure.MaxDistance value)
    {
        if (value.Horizontal == value.Vertical) return DataResult<U>.Success(ops.CreateInt(value.Horizontal));
        var builder = ops.MapBuilder();
        builder.Add("horizontal", ops.CreateInt(value.Horizontal));
        builder.Add("vertical", ops.CreateInt(value.Vertical));
        return builder.Build(ops.Empty());
    }

    private static DataResult<JigsawStructure.MaxDistance> ParseRecord<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var horizontal = ReadInt(ops, map, "horizontal");
        if (!horizontal.Result().IsPresent) return DataResult<JigsawStructure.MaxDistance>.Error(() => "max_distance_from_center 缺少 horizontal");
        var horizontalValue = horizontal.GetOrThrow();
        if (horizontalValue is < HorizontalMin or > HorizontalMax)
            return DataResult<JigsawStructure.MaxDistance>.Error(() => $"水平距离越界 必须在 {HorizontalMin}..{HorizontalMax} 之间");

        //竖直缺省取维度最大高度 对应原版 optionalFieldOf("vertical", DimensionType.Y_SIZE)
        var vertical = ReadInt(ops, map, "vertical");
        var verticalValue = vertical.Result().IsPresent ? vertical.GetOrThrow() : DimensionTypeConstants.MaxHeight;
        if (verticalValue is < 1 or > DimensionTypeConstants.MaxHeight)
            return DataResult<JigsawStructure.MaxDistance>.Error(() => $"竖直距离越界 必须在 1..{DimensionTypeConstants.MaxHeight} 之间");

        return DataResult<JigsawStructure.MaxDistance>.Success(
            new JigsawStructure.MaxDistance(horizontalValue, verticalValue));
    }

    private static DataResult<int> ReadInt<U>(DynamicOps<U> ops, MapLike<U> map, string key)
    {
        var tag = map.Get(key);
        if (!tag.IsPresent) return DataResult<int>.Error(() => $"缺少字段 {key}");
        return ops.GetNumberValue(tag.Get()).Map(value => (int)value);
    }
}

//JigsawStructureCodec 拼图结构元素 codec 对应原版 JigsawStructure.CODEC
//公共生成设置走 StructureCodecs.ReadSettings 其余字段逐个读 可选字段按原版默认值兜底
internal sealed class JigsawStructureCodec : ScalarCodec<NetCraft.Registry.Structure>
{
    public override DataResult<NetCraft.Registry.Structure> Parse<U>(DynamicOps<U> ops, U input)
    {
        var mapResult = ops.GetMap(input);
        if (!mapResult.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "拼图结构必须是对象");
        var map = mapResult.GetOrThrow();

        var settings = StructureCodecs.ReadSettings(ops, map);
        if (!settings.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "拼图结构的生成设置解析失败");

        var startPoolTag = map.Get("start_pool");
        if (!startPoolTag.IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "拼图结构缺少 start_pool");
        var startPool = StructurePoolCodecs.TemplatePoolRef.Parse(ops, startPoolTag.Get());
        if (!startPool.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "start_pool 解析失败");

        //可选字段缺失时值为空 不能靠 DataResult.Result() 判定 它对成功但值为空的情况返回不present
        if (!TryReadOptionalIdentifier(ops, map, "start_jigsaw_name", out var startJigsawName,
                out var nameError))
            return DataResult<NetCraft.Registry.Structure>.Error(() => $"start_jigsaw_name {nameError}");

        var size = StructurePlacementCodecs.ReadIntField(ops, map, "size");
        if (!size.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "拼图结构缺少 size");
        var maxDepth = size.GetOrThrow();
        if (maxDepth is < JigsawStructure.MinDepth or > JigsawStructure.MaxDepth)
            return DataResult<NetCraft.Registry.Structure>.Error(() =>
                $"size 越界 必须在 {JigsawStructure.MinDepth}..{JigsawStructure.MaxDepth} 之间 实际 {maxDepth}");

        var startHeightTag = map.Get("start_height");
        if (!startHeightTag.IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "拼图结构缺少 start_height");
        var startHeight = HeightProvider.Codec.Parse(ops, startHeightTag.Get());
        if (!startHeight.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "start_height 解析失败");

        var useExpansionHack = ReadBool(ops, map, "use_expansion_hack", false);
        if (!useExpansionHack.Result().IsPresent)
            return DataResult<NetCraft.Registry.Structure>.Error(() => "use_expansion_hack 解析失败");

        if (!TryReadOptionalHeightmap(ops, map, "project_start_to_heightmap", out var heightmap,
                out var heightmapError))
            return DataResult<NetCraft.Registry.Structure>.Error(() => $"project_start_to_heightmap {heightmapError}");

        var maxDistanceTag = map.Get("max_distance_from_center");
        if (!maxDistanceTag.IsPresent)
            return DataResult<NetCraft.Registry.Structure>.Error(() => "拼图结构缺少 max_distance_from_center");
        var maxDistance = JigsawStructure.MaxDistance.Codec.Parse(ops, maxDistanceTag.Get());
        if (!maxDistance.Result().IsPresent)
            return DataResult<NetCraft.Registry.Structure>.Error(() => "max_distance_from_center 解析失败");

        var poolAliases = ReadPoolAliases(ops, map);
        if (!poolAliases.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "pool_aliases 解析失败");

        var dimensionPadding = ReadDimensionPadding(ops, map);
        if (!dimensionPadding.Result().IsPresent)
            return DataResult<NetCraft.Registry.Structure>.Error(() => "dimension_padding 解析失败");

        var liquidSettings = ReadLiquidSettings(ops, map);
        if (!liquidSettings.Result().IsPresent) return DataResult<NetCraft.Registry.Structure>.Error(() => "liquid_settings 解析失败");

        var structure = new JigsawStructure(settings.GetOrThrow(), startPool.GetOrThrow(),
            startJigsawName, maxDepth, startHeight.GetOrThrow(), useExpansionHack.GetOrThrow(),
            heightmap, maxDistance.GetOrThrow(), poolAliases.GetOrThrow(),
            dimensionPadding.GetOrThrow(), liquidSettings.GetOrThrow());

        //水平范围连同地形适配边距不得超过 128 对应原版 verifyRange
        var edgeNeeded = structure.Settings.TerrainAdaptation.BeardEdgeNeeded();
        if (structure.MaxDistanceFromCenter.Horizontal + edgeNeeded > JigsawStructure.MaxTotalStructureRange)
            return DataResult<NetCraft.Registry.Structure>.Error(() => "水平范围连同地形适配不得超过 128");

        return DataResult<NetCraft.Registry.Structure>.Success(structure);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.Registry.Structure value)
        => DataResult<U>.Error(() => "拼图结构编码暂未实现");

    //TryReadOptionalIdentifier 读可省的标识符字段 字段缺失算成功且值留空
    private static bool TryReadOptionalIdentifier<U>(DynamicOps<U> ops, MapLike<U> map, string key,
        out Identifier? value, out string error)
    {
        value = null;
        error = string.Empty;
        var tag = map.Get(key);
        if (!tag.IsPresent) return true;
        var text = ops.GetStringValue(tag.Get());
        if (!text.Result().IsPresent)
        {
            error = "必须是字符串";
            return false;
        }
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
        {
            error = "不是合法标识符";
            return false;
        }
        value = id.Value;
        return true;
    }

    //ReadBool 读布尔字段缺失取默认值
    private static DataResult<bool> ReadBool<U>(DynamicOps<U> ops, MapLike<U> map, string key, bool defaultValue)
    {
        var tag = map.Get(key);
        return tag.IsPresent ? ops.GetBooleanValue(tag.Get()) : DataResult<bool>.Success(defaultValue);
    }

    //TryReadOptionalHeightmap 读可省的高度图类型 字段缺失算成功且值留空
    private static bool TryReadOptionalHeightmap<U>(DynamicOps<U> ops, MapLike<U> map, string key,
        out NetCraft.Registry.Heightmap.Types? value, out string error)
    {
        value = null;
        error = string.Empty;
        var tag = map.Get(key);
        if (!tag.IsPresent) return true;
        var text = ops.GetStringValue(tag.Get());
        if (!text.Result().IsPresent)
        {
            error = "必须是字符串";
            return false;
        }
        var type = NetCraft.Registry.Heightmap.FromSerializationKey(text.GetOrThrow());
        if (type is null)
        {
            error = $"未知的高度图类型 {text.GetOrThrow()}";
            return false;
        }
        value = type.Value;
        return true;
    }

    //ReadPoolAliases 读池别名列表 缺省为空 对应原版 optionalFieldOf("pool_aliases", List.of())
    private static DataResult<List<PoolAliasBinding>> ReadPoolAliases<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var tag = map.Get("pool_aliases");
        if (!tag.IsPresent) return DataResult<List<PoolAliasBinding>>.Success(new List<PoolAliasBinding>());
        var stream = ops.GetStream(tag.Get());
        if (!stream.Result().IsPresent) return DataResult<List<PoolAliasBinding>>.Error(() => "pool_aliases 必须是数组");
        var result = new List<PoolAliasBinding>();
        foreach (var element in stream.GetOrThrow())
        {
            var parsed = PoolAliasBinding.Codec.Parse(ops, element);
            if (!parsed.Result().IsPresent) return DataResult<List<PoolAliasBinding>>.Error(() => "pool_aliases 里的别名解析失败");
            result.Add(parsed.GetOrThrow());
        }
        return DataResult<List<PoolAliasBinding>>.Success(result);
    }

    //ReadDimensionPadding 读维度留白 缺省不留白
    private static DataResult<DimensionPadding> ReadDimensionPadding<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var tag = map.Get("dimension_padding");
        return tag.IsPresent
            ? DimensionPadding.Codec.Parse(ops, tag.Get())
            : DataResult<DimensionPadding>.Success(JigsawStructure.DefaultDimensionPadding);
    }

    //ReadLiquidSettings 读液体处理方式 缺省按含水方块铺
    private static DataResult<LiquidSettings> ReadLiquidSettings<U>(DynamicOps<U> ops, MapLike<U> map)
    {
        var tag = map.Get("liquid_settings");
        return tag.IsPresent
            ? StructurePoolCodecs.LiquidSettingsCodec.Parse(ops, tag.Get())
            : DataResult<LiquidSettings>.Success(JigsawStructure.DefaultLiquidSettings);
    }
}
