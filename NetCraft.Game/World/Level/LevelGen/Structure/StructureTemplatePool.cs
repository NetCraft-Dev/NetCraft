using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;
using HeightmapTypes = NetCraft.Registry.Heightmap.Types;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureTemplatePool 模板池 对应原版 net.minecraft.world.level.levelgen.structure.pools.StructureTemplatePool
//一个池 = 一组带权重的池元素 + 兜底池 拼图连接时按权重展开后随机抽一个元素
public sealed class StructureTemplatePool : NetCraft.Registry.StructureTemplatePool
{
    //SizeUnset 最大尺寸没算过时的哨兵 对应原版 SIZE_UNSET
    private const int SizeUnset = int.MinValue;

    private static Codec<StructureTemplatePool>? _directCodec;

    //DirectCodec 池自身的 codec 池引用内联定义时走它 延迟构造避免与引用 codec 初始化互相牵扯
    public static Codec<StructureTemplatePool> DirectCodec => _directCodec ??= new StructureTemplatePoolCodec();

    //ElementCodec 注册表元素 codec 注册表按标记接口持有元素
    public static readonly Codec<NetCraft.Registry.StructureTemplatePool> ElementCodec = DirectCodec.ComapFlatMap(
        pool => DataResult<NetCraft.Registry.StructureTemplatePool>.Success(pool),
        pool => (StructureTemplatePool)pool);

    private readonly List<StructurePoolElementEntry> _rawTemplates;
    private readonly List<StructurePoolElement> _templates;
    private int _maxSize = SizeUnset;

    public StructureTemplatePool(Holder<NetCraft.Registry.StructureTemplatePool> fallback,
        IReadOnlyList<StructurePoolElementEntry> templates)
    {
        _rawTemplates = new List<StructurePoolElementEntry>(templates);
        _templates = new List<StructurePoolElement>();
        //按权重把每条元素展开成模板列表 权重几就进几次
        foreach (var entry in templates)
        {
            for (var i = 0; i < entry.Weight; i++) _templates.Add(entry.Element);
        }
        Fallback = fallback;
    }

    //Fallback 池里抽不到元素时的兜底池
    public Holder<NetCraft.Registry.StructureTemplatePool> Fallback { get; }

    //RawTemplates 权重展开前的元素定义
    public IReadOnlyList<StructurePoolElementEntry> RawTemplates => _rawTemplates;

    //GetMaxSize 池里最高的元素高度 对应原版 getMaxSize 结果缓存 空池按 0
    public int GetMaxSize(StructureTemplateManager manager)
    {
        if (_maxSize != SizeUnset) return _maxSize;
        var maxSize = 0;
        foreach (var template in _templates)
        {
            if (ReferenceEquals(template, EmptyPoolElement.Instance)) continue;
            var span = template.GetBoundingBox(manager, BlockPos.Zero, Rotation.None).LengthY;
            if (span > maxSize) maxSize = span;
        }
        _maxSize = maxSize;
        return _maxSize;
    }

    //GetRandomTemplate 权重列表里随机抽一个 空池给空元素 对应原版 getRandomTemplate
    public StructurePoolElement GetRandomTemplate(RandomSource random)
        => _templates.Count == 0 ? EmptyPoolElement.Instance : _templates[random.NextInt(_templates.Count)];

    //GetShuffledTemplates 权重列表的洗牌副本 对应原版 getShuffledTemplates
    public List<StructurePoolElement> GetShuffledTemplates(RandomSource random)
        => RandomCollections.ShuffledCopy(_templates, random);

    //Size 权重展开后的元素个数 对应原版 size
    public int Size() => _templates.Count;

    public override string ToString() => $"StructureTemplatePool[{_templates.Count} 项 兜底 {Fallback.UnwrapKey()?.Identifier}]";

    //Projection 投影 决定放置时要不要把结构贴到地表 对应原版 StructureTemplatePool.Projection
    public enum Projection
    {
        TerrainMatching,
        Rigid,
    }
}

//StructurePoolElementEntry 池里的一条元素定义 对应原版 Pair<StructurePoolElement, Integer>
//weight 是正整数权重 原版限定 1..150
public sealed record StructurePoolElementEntry(StructurePoolElement Element, int Weight);

//PoolProjections 投影的名字映射与自带处理器 对应原版 Projection 的两个枚举常量
internal static class PoolProjections
{
    //terrain_matching 投影要把结构贴到地表 自带一个向下抬 1 格的引力处理器
    private static readonly IReadOnlyList<StructureProcessor> TerrainMatchingProcessors =
        new StructureProcessor[] { new GravityProcessor(HeightmapTypes.WorldSurfaceWg, -1) };

    private static readonly IReadOnlyList<StructureProcessor> RigidProcessors = Array.Empty<StructureProcessor>();

    //Name 取 JSON 名 对应原版 getSerializedName
    public static string Name(StructureTemplatePool.Projection projection)
        => projection == StructureTemplatePool.Projection.TerrainMatching ? "terrain_matching" : "rigid";

    //TryParse 按 JSON 名解析 非法返回 null
    public static StructureTemplatePool.Projection? TryParse(string name) => name switch
    {
        "terrain_matching" => StructureTemplatePool.Projection.TerrainMatching,
        "rigid" => StructureTemplatePool.Projection.Rigid,
        _ => null,
    };

    //GetProcessors 该投影自带的处理器 对应原版 Projection.getProcessors
    public static IReadOnlyList<StructureProcessor> GetProcessors(StructureTemplatePool.Projection projection)
        => projection == StructureTemplatePool.Projection.TerrainMatching ? TerrainMatchingProcessors : RigidProcessors;
}

//StructureTemplatePoolCodec 模板池 codec 解 fallback 与按权重列表 对应原版 DIRECT_CODEC
internal sealed class StructureTemplatePoolCodec : ScalarCodec<StructureTemplatePool>
{
    //WeightMin/WeightMax weight 的允许区间 对应原版 Codec.intRange(1, 150)
    private const int WeightMin = 1;
    private const int WeightMax = 150;

    public override DataResult<StructureTemplatePool> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePool(ops, map));

    private static DataResult<StructureTemplatePool> DecodePool<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var fallbackTag = input.Get("fallback");
        if (!fallbackTag.IsPresent) return DataResult<StructureTemplatePool>.Error(() => "模板池缺少 fallback");
        var fallback = StructurePoolCodecs.TemplatePoolRef.Parse(ops, fallbackTag.Get());
        if (!fallback.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "模板池的 fallback 解析失败");

        var elementsTag = input.Get("elements");
        if (!elementsTag.IsPresent) return DataResult<StructureTemplatePool>.Error(() => "模板池缺少 elements");
        var stream = ops.GetStream(elementsTag.Get());
        if (!stream.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements 必须是数组");

        var entries = new List<StructurePoolElementEntry>();
        foreach (var item in stream.GetOrThrow())
        {
            var itemMap = ops.GetMap(item);
            if (!itemMap.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements 的元素必须是对象");
            var map = itemMap.GetOrThrow();

            var elementTag = map.Get("element");
            if (!elementTag.IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements 的元素缺少 element");
            var elementError = string.Empty;
            var element = StructurePoolElement.Codec.Parse(ops, elementTag.Get());
            var elementValue = element.ResultOrPartial(message => elementError = message);
            if (!elementValue.IsPresent)
                return DataResult<StructureTemplatePool>.Error(() => $"elements 的 element 解析失败 {elementError}");

            var weight = StructurePlacementCodecs.ReadIntField(ops, map, "weight");
            if (!weight.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements 的元素缺少 weight");
            var value = weight.GetOrThrow();
            if (value < WeightMin || value > WeightMax)
                return DataResult<StructureTemplatePool>.Error(() => $"weight 越界 必须在 {WeightMin}..{WeightMax} 之间 实际 {value}");

            entries.Add(new StructurePoolElementEntry(elementValue.Get(), value));
        }

        return DataResult<StructureTemplatePool>.Success(new StructureTemplatePool(fallback.GetOrThrow(), entries));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructureTemplatePool value)
        => DataResult<U>.Error(() => "模板池编码暂未实现");
}
