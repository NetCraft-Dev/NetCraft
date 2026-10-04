using NetCraft.Game.World.Level.LevelGen.Structure;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util.Random;
using HeightmapRegistry = NetCraft.Registry.Heightmap;
using LevelHeightmap = NetCraft.Storage.LevelGen.Heightmap;

namespace NetCraft.Game.World.Level.LevelGen;

//ChunkStatusProcessor 区块状态机处理器对应原版 ChunkStatus 状态机流水线
//阶段 E 简化实现按 ChunkStatus 名称调用 ChunkGenerator 对应方法
//阶段 11.54-B 接入 STRUCTURE_START/STRUCTURE_REFERENCES 真实结构生成
public sealed class ChunkStatusProcessor
{
    private readonly ChunkGenerator _generator;
    private readonly RandomSource _random;
    private readonly long _seed;
    //_chunkProvider 邻块提供者 装饰阶段要收集 3x3 内的群系 拿不到邻块时只按中心区块装饰
    private readonly Func<int, int, ChunkAccess?>? _chunkProvider;

    //FinalHeightmaps 最终高度图集合对应原版 ChunkStatus.FINAL_HEIGHTMAPS
    //OCEAN_FLOOR 只算阻挡移动的实心方块 其余三种含流体与透光方块
    private static readonly HeightmapRegistry.Types[] FinalHeightmaps =
    {
        HeightmapRegistry.Types.OceanFloor,
        HeightmapRegistry.Types.WorldSurface,
        HeightmapRegistry.Types.MotionBlocking,
        HeightmapRegistry.Types.MotionBlockingNoLeaves,
    };
    //StructureFeatures 结构管理器 持本流水线装配出的结构与引用供后续阶段查询
    public StructureFeatureManager StructureFeatures { get; }

    //DecorationWriteRadius 装饰阶段的可写区块半径 1 即中心区块连同八邻
    //对应原版 WorldGenRegion 在 FEATURES 阶段的写半径
    private const int DecorationWriteRadius = 1;

    public ChunkStatusProcessor(ChunkGenerator generator, RandomSource random)
        : this(generator, random, null, 0L) { }

    //带结构集合的构造函数供结构装配场景使用
    //structureRegistry 为 null 时结构阶段空跑 seed 决定雕刻器与结构放置的种子派生
    //sharedStructures 由生成链整个维度共享 邻块装配出的结构本区块装饰时才能被看到
    public ChunkStatusProcessor(ChunkGenerator generator, RandomSource random,
        StructurePlacementRegistry? structureRegistry, long seed, Func<int, int, ChunkAccess?>? chunkProvider = null,
        StructureFeatureManager? sharedStructures = null)
    {
        _generator = generator;
        _random = random;
        _seed = seed;
        _chunkProvider = chunkProvider;
        StructureFeatures = sharedStructures
            ?? (structureRegistry is null
                ? new StructureFeatureManager()
                : new StructureFeatureManager(generator, structureRegistry));
    }

    //ProcessChunk 按 ChunkStatus 调用对应生成阶段
    //返回 true 表示该状态已处理false 表示无对应处理
    public bool ProcessChunk(ChunkAccess chunk, ChunkStatus status)
    {
        var structures = StructureManager.Default;
        if (status == ChunkStatus.STRUCTURE_START)
        {
            //STRUCTURE_START 按结构集合装配本区块命中的结构
            StructureFeatures.CreateStarts(chunk);
            return true;
        }
        if (status == ChunkStatus.STRUCTURE_REFERENCES)
        {
            //STRUCTURE_REFERENCES 扫 17x17 邻居收集包围盒覆盖当前 chunk 的结构引用
            //供后续 FEATURES 阶段避让装饰
            StructureFeatures.CollectReferences(chunk.Pos);
            return true;
        }
        if (status == ChunkStatus.BIOMES)
        {
            //按 quart 遍历每个 section 写入 BiomeSource 查询的 biome 对应原版点采样
            var biomeSource = _generator.BiomeSource;
            var posX = chunk.Pos.X;
            var posZ = chunk.Pos.Z;
            for (var sectionIdx = 0; sectionIdx < chunk.SectionsCount; sectionIdx++)
            {
                var sectionY = chunk.MinSectionY + sectionIdx;
                for (var qY = 0; qY < 4; qY++)
                {
                    for (var qX = 0; qX < 4; qX++)
                    {
                        for (var qZ = 0; qZ < 4; qZ++)
                        {
                            var wx = posX * 16 + qX * 4;
                            var wy = sectionY * 16 + qY * 4;
                            var wz = posZ * 16 + qZ * 4;
                            var biome = biomeSource.GetBiome(wx, wy, wz);
                            chunk.SetBiome(wx, wy, wz, Holder<Biome>.Direct(biome));
                        }
                    }
                }
            }
            return true;
        }
        if (status == ChunkStatus.NOISE)
        {
            //噪声阶段调用 FillFromNoise 填方块
            _generator.FillFromNoise(new object(), structures, chunk, _random);
            return true;
        }
        if (status == ChunkStatus.SURFACE)
        {
            //表面阶段调用 BuildSurface 应用表面规则
            _generator.BuildSurface(new object(), structures, chunk, _random);
            return true;
        }
        if (status == ChunkStatus.CARVERS)
        {
            //雕刻阶段按生物群系配置的 carvers 挖洞穴 液体雕刻已合并进本阶段
            _generator.ApplyCarvers(_seed, chunk, _random);
            return true;
        }
        if (status == ChunkStatus.LIQUID_CARVERS)
        {
            //液体雕刻已被合并到 CARVERS 原版此阶段为空
            return true;
        }
        if (status == ChunkStatus.FEATURES)
        {
            //装饰前先把最终高度图整体算出来 对应原版 ChunkStatusTasks.generateFeatures 开头
            //特色放置与出生点列查找都依赖它
            LevelHeightmap.PrimeHeightmaps(chunk, FinalHeightmaps);
            //装饰只能写中心区块及其邻居 越界静默丢弃 对应原版 WorldGenRegion 的写半径
            //结构落地与特征放置都在这个区域内进行 结构先于特征
            var region = new WorldGenRegion(chunk.MinSectionY, chunk.SectionsCount)
            {
                Seed = _seed,
                CenterChunk = chunk.Pos,
                WriteRadius = DecorationWriteRadius,
            };
            region.AddChunk(chunk);
            if (_chunkProvider is not null)
            {
                for (var offsetX = -DecorationWriteRadius; offsetX <= DecorationWriteRadius; offsetX++)
                for (var offsetZ = -DecorationWriteRadius; offsetZ <= DecorationWriteRadius; offsetZ++)
                {
                    if (offsetX == 0 && offsetZ == 0) continue;
                    var neighbour = _chunkProvider(chunk.Pos.X + offsetX, chunk.Pos.Z + offsetZ);
                    if (neighbour is not null) region.AddChunk(neighbour);
                }
            }
            _generator.ApplyBiomeDecoration(region, chunk, StructureFeatures);
            return true;
        }
        if (status == ChunkStatus.LIGHT)
        {
            //LIGHT 阶段的光照计算由 ServerChunkCache.ProcessLight 在区块就绪后统一执行
            //此处不重复计算避免同一区块跑两遍光照
            return true;
        }
        if (status == ChunkStatus.SPAWN || status == ChunkStatus.HEIGHTMAPS)
        {
            //生物生成占位 真实接入需对应子系统就绪
            //高度图不在此处理: 原版 26.2 已无独立 HEIGHTMAPS 阶段 高度图由 FEATURES 阶段前
            //按 FINAL_HEIGHTMAPS 整体补算 加上 GetHeight 的惰性补算与方块变更的增量维护覆盖
            return true;
        }
        if (status == ChunkStatus.EMPTY || status == ChunkStatus.FULL)
        {
            //EMPTY 与 FULL 无生成任务
            return true;
        }
        return false;
    }

    //ProcessToStatus 把区块从当前状态推进到目标状态
    //按 ChunkStatus 注册顺序依次调用 ProcessChunk 直到达到 target
    public void ProcessToStatus(ChunkAccess chunk, ChunkStatus target)
    {
        var current = chunk.ChunkStatus;
        while (current != target && current is not null)
        {
            var next = NextStatus(current);
            if (next is null) break;
            ProcessChunk(chunk, next);
            current = next;
        }
    }

    //StatusOrder 阶段推进顺序 静态化避免每次 NextStatus 都新建数组
    private static readonly ChunkStatus[] StatusOrder =
    {
        ChunkStatus.EMPTY,
        ChunkStatus.STRUCTURE_START,
        ChunkStatus.STRUCTURE_REFERENCES,
        ChunkStatus.BIOMES,
        ChunkStatus.NOISE,
        ChunkStatus.SURFACE,
        ChunkStatus.CARVERS,
        ChunkStatus.LIQUID_CARVERS,
        ChunkStatus.FEATURES,
        ChunkStatus.LIGHT,
        ChunkStatus.SPAWN,
        ChunkStatus.HEIGHTMAPS,
        ChunkStatus.FULL
    };

    //NextStatus 查询下一个状态按 ChunkStatus 静态注册顺序
    private static ChunkStatus? NextStatus(ChunkStatus status)
    {
        var index = System.Array.IndexOf(StatusOrder, status);
        return index >= 0 && index < StatusOrder.Length - 1 ? StatusOrder[index + 1] : null;
    }

}
