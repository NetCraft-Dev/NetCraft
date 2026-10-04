using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureType 结构类型 对应原版 net.minecraft.world.level.levelgen.structure.StructureType
//只承载注册名与元素 codec 装载时按 type 字段把 JSON 派发到具体结构的 codec
public sealed class StructureType : NetCraft.Registry.StructureType<object>
{
    public Identifier Id { get; }

    //ElementCodec 该类型结构的元素 codec 解析含生成设置的整份 JSON
    //测试与程序化结构不参与数据驱动装载 可以为 null
    public Codec<NetCraft.Registry.Structure>? ElementCodec { get; }

    public StructureType(Identifier id, Codec<NetCraft.Registry.Structure>? elementCodec = null)
    {
        Id = id;
        ElementCodec = elementCodec;
    }

    public override string ToString() => $"StructureType[{Id}]";
}

//Structure 结构抽象 对应原版 net.minecraft.world.level.levelgen.structure.Structure
//一个结构 = 生成设置 + 查找生成点的逻辑 装配出的片段由 StructureStart 承载
//真正把这些片段写进世界的是装饰阶段的 placeInChunk 不是这里
public abstract class Structure : NetCraft.Registry.Structure
{
    public abstract Identifier Id { get; }

    //Type 结构类型 供注册表派发与序列化回写
    public abstract StructureType Type { get; }

    //Settings 生成设置 群系范围与装饰步骤都在这里
    public StructureGenerationSettings Settings { get; }

    protected Structure(StructureGenerationSettings settings) => Settings = settings;

    //FindGenerationPoint 查找生成点 对应原版 findGenerationPoint
    //返回 null 表示该区块不生成
    public abstract GenerationStub? FindGenerationPoint(GenerationContext context);

    //Generate 装配出 StructureStart 对应原版 generate
    //群系过滤由 context.ValidBiome 承担 未通过的直接判无效
    public StructureStart Generate(GenerationContext context)
    {
        var stub = FindGenerationPoint(context);
        if (stub is null) return StructureStart.Invalid;
        if (!context.ValidBiome(stub.Position)) return StructureStart.Invalid;
        var start = new StructureStart(this, context.ChunkPos, stub.Build());
        return start.IsValid ? start : StructureStart.Invalid;
    }
}

//StructurePiecesBuilder 片段收集器 对应原版 StructurePiecesBuilder
//生成点只负责往里塞片段 装配完成后一次性取出
public sealed class StructurePiecesBuilder
{
    private readonly List<StructurePiece> _pieces = new();

    public bool IsEmpty => _pieces.Count == 0;

    public int Count => _pieces.Count;

    //AddPiece 追加片段
    public void AddPiece(StructurePiece piece) => _pieces.Add(piece);

    //Build 取出片段列表
    public IReadOnlyList<StructurePiece> Build() => _pieces;
}

//GenerationStub 生成点 对应原版 Structure.GenerationStub
//持结构原点与片段来源 片段既可以直接给列表也可以延迟到 Build 时才生成
public sealed class GenerationStub
{
    //Position 结构原点 群系过滤与后续对齐都以它为准
    public BlockPos Position { get; }

    private readonly IReadOnlyList<StructurePiece>? _pieces;
    private readonly Action<StructurePiecesBuilder>? _generator;

    public GenerationStub(BlockPos position, IReadOnlyList<StructurePiece> pieces)
    {
        Position = position;
        _pieces = pieces;
    }

    public GenerationStub(BlockPos position, Action<StructurePiecesBuilder> generator)
    {
        Position = position;
        _generator = generator;
    }

    //Build 产出片段列表 直接给列表的走快路径
    public IReadOnlyList<StructurePiece> Build()
    {
        if (_pieces is not null) return _pieces;
        var builder = new StructurePiecesBuilder();
        _generator!(builder);
        return builder.Build();
    }
}

//GenerationContext 结构生成上下文 对应原版 Structure.GenerationContext
//把生成器 种子 区块与随机源打包给结构 随机源按种子与区块坐标派生保证可重现
public sealed class GenerationContext
{
    public ChunkGenerator ChunkGenerator { get; }
    public long Seed { get; }
    public ChunkPos ChunkPos { get; }

    //Random 结构生成用随机源 对应原版 WorldgenRandom 基于 LegacyRandomSource
    public RandomSource Random { get; }

    public LevelHeightAccessor HeightAccessor { get; }

    //ValidBiome 群系过滤 对应原版 findValidGenerationPoint 里的谓词
    //默认放行 由调用方按结构声明群系替换 测试与不走群系约束的场景用默认
    public Func<BlockPos, bool> ValidBiome { get; set; } = _ => true;

    public GenerationContext(ChunkGenerator chunkGenerator, long seed, ChunkPos chunkPos,
        LevelHeightAccessor heightAccessor)
    {
        ChunkGenerator = chunkGenerator;
        Seed = seed;
        ChunkPos = chunkPos;
        HeightAccessor = heightAccessor;
        var random = new LegacyRandomSource(0L);
        WorldgenRandom.SetLargeFeatureSeed(random, seed, chunkPos.X, chunkPos.Z);
        Random = random;
    }

    //IsBiomeAllowed 按结构声明的群系判定 未声明群系视为不允许
    public bool IsBiomeAllowed(Structure structure, BlockPos pos)
    {
        if (structure.Settings.Biomes.Size == 0) return false;
        var current = ChunkGenerator.BiomeSource.GetBiome(pos.X, pos.Y, pos.Z);
        foreach (var holder in structure.Settings.Biomes)
        {
            if (holder.Value is { } biome && biome.Id == current.Id) return true;
        }
        return false;
    }
}
