using System.Runtime.CompilerServices;
using NetCraft.Game.Bootstrap;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.LevelGen.Carver;
using NetCraft.Game.World.Level.LevelGen.Synth;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;
using NetCraft.Util.Random;
using GameConfiguredWorldCarver = NetCraft.Game.World.Level.LevelGen.Carver.ConfiguredWorldCarver;
using GameStructure = NetCraft.Game.World.Level.LevelGen.Structure.Structure;
using GameStructureFeatureManager = NetCraft.Game.World.Level.LevelGen.Structure.StructureFeatureManager;
using GameGenerationStep = NetCraft.Game.World.Level.LevelGen.Features.GenerationStep;
using GamePlacedFeature = NetCraft.Game.World.Level.LevelGen.Placement.PlacedFeature;
using GameBeardifier = NetCraft.Game.World.Level.LevelGen.Structure.Beardifier;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseBasedChunkGenerator 基于噪声的区块生成器对应原版 net.minecraft.world.level.levelgen.NoiseBasedChunkGenerator
//继承 ChunkGenerator 持有 NoiseGeneratorSettings 与 PerlinNoise 基础噪声
//P0 接入 RandomState + NoiseChunk + Aquifer 通过 GetInterpolatedState 驱动方块决策
//内核不硬编码方块 DefaultBlock/DefaultFluid 由 Game 层通过 NoiseGeneratorSettings 注入
public class NoiseBasedChunkGenerator : ChunkGenerator
{
    public NoiseGeneratorSettings Settings { get; }

    //RandomState 按世界种子创建一次后整局复用 对齐原版 RandomState 的世界级单例语义
    //密度树 mapAll 与 NormalNoise 实例化开销大,逐区块重建既慢又会让相邻区块噪声不连续
    //volatile: 双检锁的另一半,没有它别的线程可能看到非 null 但尚未构造完的对象
    private volatile RandomState? _cachedRandomState;
    private readonly object _randomStateLock = new();

    //NoiseChunk 按区块挂一份 噪声阶段建好地表阶段直接复用 对应原版 protoChunk.getOrCreateNoiseChunk
    //用弱表避免区块卸载后 NoiseChunk 跟着泄漏
    private readonly ConditionalWeakTable<ChunkAccess, NoiseChunk> _noiseChunks = new();

    //SamplerHeightNoise 用于地形基础高度采样的 NormalNoise 占位
    //真实接入时由 RandomState 派生此处简化为可空字段
    public NormalNoise? HeightNoise { get; private set; }

    public NoiseBasedChunkGenerator(BiomeSource biomeSource, NoiseGeneratorSettings settings)
        : base(WithSamplerIfNeeded(biomeSource, settings))
    {
        Settings = settings;
    }

    //WithSamplerIfNeeded 构造前检查 BiomeSource 是否为未注入采样器的实现
    //多噪声源用 Settings.NoiseRouter 建 NoiseRouterSampler 末地源注入 erosion 槽的密度函数
    //ParameterList 为 null 时不注入因为 GetBiome 仍走 Biome.Plains 占位路径
    private static BiomeSource WithSamplerIfNeeded(BiomeSource biomeSource, NoiseGeneratorSettings settings)
    {
        if (biomeSource is MultiNoiseBiomeSource { Sampler: null, ParameterList: not null } multi)
            return new MultiNoiseBiomeSource(multi.ParameterList!, new Climate.NoiseRouterSampler(settings.NoiseRouter));
        //末地的 erosion 槽读的是 end_islands 与主世界语义不同 直接取噪声设置里的那个槽
        if (biomeSource is TheEndBiomeSource { ErosionNoise: null } theEnd)
            return theEnd.WithErosion(settings.NoiseRouter.Erosion);
        return biomeSource;
    }

    //InitHeightNoise 派生 NormalNoise 实例用于地形高度采样
    //firstOctave/amplitudes 对齐原版默认配置简化版用 -3 与 [1,1,1,1,1,1,1] 占位
    public void InitHeightNoise(RandomSource random)
    {
        Log.Debug($"InitHeightNoise entry random={random}");
        HeightNoise = new NormalNoise(random, -3, 1, 1, 1, 1, 1, 1, 1);
        //Log.Debug("InitHeightNoise 出口");
    }

    //GetGenDepth 返回 settings 推导的最大生成深度对应原版 getGenDepth
    //用 Settings.NoiseSettings.Height 对齐原版 settings.height
    public override int GetGenDepth() => Settings.NoiseSettings.Height;

    //GetMinY 返回噪声设置的最低位对应原版 getMinY
    public override int GetMinY() => Settings.NoiseSettings.MinY;

    //GetBaseHeight 采样指定坐标的基础高度对应原版 getBaseHeight
    //type 参数为 HeightmapTypes 占位用 int简化版调 HeightNoise 采样
    public override int GetBaseHeight(int x, int z, int type, LevelHeightAccessor level, RandomSource random)
    {
        Log.Debug($"GetBaseHeight entry x={x} z={z} type={type} level={level} random={random}");
        if (HeightNoise is null) GetOrCreateRandomState();
        var value = HeightNoise!.GetValue(x, 0, z);
        var raw = (int)Math.Round(value * 32 + 64);
        var result = Math.Clamp(raw, level.MinBuildHeight, level.MaxBuildHeight - 1);
        Log.Debug($"GetBaseHeight exit result={result}");
        return result;
    }

    //GetBaseColumn 采样指定坐标的基础列方块状态对应原版 getBaseColumn
    //简化返回 object[] 长度为 SectionsCount*16 内容全 null 待 BlockState 接入
    public override object[] GetBaseColumn(int x, int z, LevelHeightAccessor level, RandomSource random)
    {
        Log.Debug($"GetBaseColumn entry x={x} z={z} level={level} random={random}");
        var column = new object?[level.SectionsCount * 16];
        var surfaceY = GetBaseHeight(x, z, 0, level, random);
        for (var y = 0; y < column.Length; y++)
        {
            var absoluteY = level.MinBuildHeight + y;
            column[y] = absoluteY < surfaceY - 4 ? "stone" : null;
        }
        var result = column!;
        Log.Debug($"GetBaseColumn exit result={result.Length}");
        return result;
    }

    //CreateFluidPicker 构造全局流体选择器对应原版 NoiseBasedChunkGenerator.createFluidPicker
    //lavaStatus 深岩浆 y<-54 返回 lava seaStatus 海平面流体 y<seaLevel 返回水
    //emptyStatus 占位 AIR 防御性兜底原版用 DimensionType.MIN_Y*2 此处用 int.MinValue/2 更极端
    //FluidPicker 是 Game 层注入方块的边界点 Blocks.LAVA/AIR 由 Game 层提供
    public static Aquifer.FluidPicker CreateFluidPicker(NoiseGeneratorSettings settings)
    {
        var lavaStatus = new FluidStatus(-54, Blocks.LAVA.DefaultBlockState);
        var seaLevel = settings.SeaLevel;
        var seaStatus = new FluidStatus(seaLevel, settings.DefaultFluid);
        var emptyStatus = new FluidStatus(int.MinValue / 2, Blocks.AIR.DefaultBlockState);
        return new GlobalFluidPicker(lavaStatus, seaStatus, emptyStatus, seaLevel);
    }

    //FillFromNoise 从噪声填方块到区块对应原版 fillFromNoise
    //逐 cell 流式推进: 角点只采样一次 格内方块由插值节点给出密度
    //密度>0 时 Aquifer 返回 null 用 Settings.DefaultBlock 兜底即石头
    //密度<=0 时 Aquifer 返回流体状态海平面以下水更深处岩浆否则空气
    public override void FillFromNoise(object blender, object structures, ChunkAccess chunk, RandomSource random)
    {
        if (chunk is not ProtoChunk proto) return;

        //首次调用触发 Game 层 Bootstrap 注册方块与噪声参数后续调用幂等返回
        GameBootstrap.Bootstrap();

        var randomState = GetOrCreateRandomState();
        //结构在 STRUCTURE_START 阶段已装配好 噪声阶段据此把结构范围内的地形顶平
        var beardifier = structures is GameStructureFeatureManager manager
            ? GameBeardifier.ForStructuresInChunk(manager, chunk.Pos)
            : GameBeardifier.Empty;
        var noiseChunk = GetOrCreateNoiseChunk(chunk, randomState, beardifier);
        var defaultBlock = Settings.DefaultBlock;

        var cellWidth = noiseChunk.CellWidth;
        var cellHeight = noiseChunk.CellHeight;
        var cellMinY = noiseChunk.CellNoiseMinY;
        var cellCountY = noiseChunk.CellCountY;
        var cellCountXZ = 16 / cellWidth;
        var chunkStartBlockX = chunk.Pos.MinBlockX;
        var chunkStartBlockZ = chunk.Pos.MinBlockZ;

        noiseChunk.InitializeForFirstCellX();
        for (var cellXIndex = 0; cellXIndex < cellCountXZ; cellXIndex++)
        {
            noiseChunk.AdvanceCellX(cellXIndex);
            for (var cellZIndex = 0; cellZIndex < cellCountXZ; cellZIndex++)
            {
                for (var cellYIndex = cellCountY - 1; cellYIndex >= 0; cellYIndex--)
                {
                    noiseChunk.SelectCellYZ(cellYIndex, cellZIndex);
                    for (var yInCell = cellHeight - 1; yInCell >= 0; yInCell--)
                    {
                        var posY = (cellMinY + cellYIndex) * cellHeight + yInCell;
                        var sectionY = posY >> 4;
                        noiseChunk.UpdateForY(posY, yInCell / (double)cellHeight);
                        for (var xInCell = 0; xInCell < cellWidth; xInCell++)
                        {
                            var posX = chunkStartBlockX + cellXIndex * cellWidth + xInCell;
                            noiseChunk.UpdateForX(posX, xInCell / (double)cellWidth);
                            for (var zInCell = 0; zInCell < cellWidth; zInCell++)
                            {
                                var posZ = chunkStartBlockZ + cellZIndex * cellWidth + zInCell;
                                noiseChunk.UpdateForZ(posZ, zInCell / (double)cellWidth);
                                var state = noiseChunk.GetInterpolatedState() ?? defaultBlock;
                                proto.SetBlockState(sectionY, posX & 15, posY & 15, posZ & 15, state);
                            }
                        }
                    }
                }
            }
            noiseChunk.SwapSlices();
        }
        noiseChunk.StopInterpolation();
    }

    //GetOrCreateRandomState 按世界种子创建并缓存 RandomState 对应原版世界级单例
    //种子取 WorldSeed 而不是调用方传进来的随机源: 传进来的是每区块派生的局部随机源
    //谁先抢到初始化谁就定下整局地形 同一个世界种子每跑一次地表高度都会不一样
    private RandomState GetOrCreateRandomState()
    {
        var cached = _cachedRandomState;
        if (cached is not null) return cached;
        lock (_randomStateLock)
        {
            if (_cachedRandomState is null)
            {
                _cachedRandomState = RandomState.Create(Settings, BuiltInRegistries.NOISE, WorldSeed);
                //高度噪声同样按世界种子派生 并发下按传入随机源建会随线程调度漂
                InitHeightNoise(RandomSource.Create(WorldSeed));
            }
            return _cachedRandomState;
        }
    }

    //FindSpawnPosition 按噪声设置的 spawn_target 做气候径向搜索对应原版 Climate.Sampler.findSpawnPosition
    //数据包没给 spawn_target 时返回 null 由调用方退回默认落点
    public BlockPos? FindSpawnPosition()
    {
        if (Settings.SpawnTarget.Count == 0) return null;
        var result = Climate.FindSpawnPosition(Settings.SpawnTarget, GetOrCreateRandomState().Sampler);
        Log.Debug($"FindSpawnPosition exit result={result}");
        return result;
    }

    //GetOrCreateNoiseChunk 取该区块的 NoiseChunk 没有就建一个对应原版 getOrCreateNoiseChunk
    private NoiseChunk GetOrCreateNoiseChunk(ChunkAccess chunk, RandomState randomState, DensityFunction beardifier)
    {
        if (_noiseChunks.TryGetValue(chunk, out var existing)) return existing;
        var created = new NoiseChunk(chunk, randomState, Settings, beardifier);
        _noiseChunks.Add(chunk, created);
        return created;
    }

    //BuildSurface 应用表面规则到区块对应原版 buildSurface
    //规则树取 settings 的 surface_rule 基岩层与地表材质都由它决定 没有规则时直接跳过
    public override void BuildSurface(object region, object structures, ChunkAccess chunk, RandomSource random)
    {
        Log.Debug($"BuildSurface entry chunk={chunk.Pos} random={random}");
        GameBootstrap.Bootstrap();
        var ruleSource = Settings.SurfaceRule;
        if (ruleSource is null)
        {
            Log.Debug("BuildSurface exit, no surface_rule");
            return;
        }
        var randomState = GetOrCreateRandomState();
        //噪声阶段已按结构建好 NoiseChunk 这里通常直接命中缓存 回退时用恒零标记
        var noiseChunk = GetOrCreateNoiseChunk(chunk, randomState, BeardifierMarker.Instance);
        //地表阶段的群系查询走 BiomeManager: 命中本区块调色板就是一次查表 出界才回退完整采样
        //直接传 GetBiome 会让每一格都做一次六维气候采样 区块生成慢到连接超时
        var biomeManager = new BiomeManager(chunk, GetBiome, randomState.Seed);
        randomState.SurfaceSystem.BuildSurface(chunk, noiseChunk, ruleSource, biomeManager.GetBiome,
            chunk.MinSectionY * 16, GetGenDepth(), Settings.UseLegacyRandomSource);
        //Log.Debug("BuildSurface 出口");
    }

    //ApplyCarvers 在地表之后装饰之前雕刻洞穴对应原版 applyCarvers
    //遍历中心区块周围 17x17 个区块只为取各位置的生物群系配置 雕刻目标与掩码始终是中心区块
    public override void ApplyCarvers(long seed, ChunkAccess chunk, RandomSource random)
    {
        if (chunk is not ProtoChunk proto) return;
        GameBootstrap.Bootstrap();
        var randomState = GetOrCreateRandomState();
        var noiseChunk = GetOrCreateNoiseChunk(chunk, randomState, BeardifierMarker.Instance);
        var context = new CarvingContext(this, chunk, noiseChunk, randomState, Settings.SurfaceRule);
        var mask = proto.GetOrCreateCarvingMask();
        Func<int, int, int, Biome> biomeGetter = GetBiome;
        var carverRandom = RandomSource.Create(seed);
        var pos = chunk.Pos;
        for (var dx = -8; dx <= 8; dx++)
        {
            for (var dz = -8; dz <= 8; dz++)
            {
                var sourcePos = new ChunkPos(pos.X + dx, pos.Z + dz);
                var carvers = BiomeSource.GetBiome(sourcePos.MinBlockX, 0, sourcePos.MinBlockZ).Generation.Carvers;
                var index = 0;
                foreach (var holder in carvers)
                {
                    if (holder.IsBound())
                    {
                        var carver = (GameConfiguredWorldCarver)holder.Value;
                        SetLargeFeatureSeed(carverRandom, seed + index, sourcePos.X, sourcePos.Z);
                        if (carver.IsStartChunk(carverRandom))
                            carver.Carve(context, chunk, biomeGetter, carverRandom, noiseChunk.Aquifer, sourcePos, mask);
                    }
                    index++;
                }
            }
        }
    }

    //SetLargeFeatureSeed 按区块坐标重播随机源对应原版 WorldgenRandom.setLargeFeatureSeed
    private static void SetLargeFeatureSeed(RandomSource random, long seed, int chunkX, int chunkZ)
    {
        random.SetSeed(seed);
        var xScale = random.NextLong() | 1L;
        var zScale = random.NextLong() | 1L;
        random.SetSeed(chunkX * xScale ^ chunkZ * zScale ^ seed);
    }

    //ApplyBiomeDecoration 应用生物群系装饰对应原版 applyBiomeDecoration
    //按 11 个装饰步骤推进 每步内先落结构再放特征 结构先落地特征才能长在它上面
    //只处理 3x3 内实际出现且属于本源可能群系的那些特征
    public override void ApplyBiomeDecoration(WorldGenRegion region, ChunkAccess chunk,
        GameStructureFeatureManager structures)
    {
        var centerPos = chunk.Pos;
        var origin = new BlockPos(centerPos.X * 16, region.MinSectionY * 16, centerPos.Z * 16);
        var featureList = FeaturesPerStep;
        var random = new XoroshiroRandomSource(RandomSupport.GenerateUniqueSeed());
        var decorationSeed = WorldgenRandom.SetDecorationSeed(random, region.Seed, origin.X, origin.Z);

        //3x3 内所有 section 的群系调色板整体枚举再按本源可能群系过滤
        //邻块可能是别的地形源本区块不该替它装饰
        var biomes = new HashSet<Biome>(ReferenceEqualityComparer.Instance);
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
        {
            var neighbour = region.GetChunk(centerPos.X + offsetX, centerPos.Z + offsetZ);
            if (neighbour is null) continue;
            for (var sectionIndex = 0; sectionIndex < neighbour.SectionsCount; sectionIndex++)
            {
                neighbour.GetSection(neighbour.MinSectionY + sectionIndex)?.GetBiomes()
                    .GetAll(holder => { if (holder.IsBound()) biomes.Add(holder.Value); });
            }
        }
        biomes.IntersectWith(BiomeSource.PossibleBiomes);

        var structuresByStep = GroupStructuresByStep();
        var featureStepCount = featureList.Count;
        var generationSteps = Math.Max(GameGenerationStep.Count, featureStepCount);
        for (var stepIndex = 0; stepIndex < generationSteps; stepIndex++)
        {
            //结构喂 setFeatureSeed 的是步内顺序号 特征喂的是步内全局索引 两者语义不同
            var index = 0;
            if (structures.ShouldGenerateStructures
                && structuresByStep.TryGetValue(stepIndex, out var stepStructures))
            {
                foreach (var structureId in stepStructures)
                {
                    WorldgenRandom.SetFeatureSeed(random, decorationSeed, index, stepIndex);
                    foreach (var start in structures.StartsForStructure(centerPos, structureId))
                        start.PlaceInChunk(region, centerPos.X, centerPos.Z);
                    index++;
                }
            }

            if (stepIndex >= featureStepCount) continue;
            var stepData = featureList[stepIndex];
            //先收齐本步要放的全局索引再去重排序 顺序必须与排序器的拓扑序一致
            var candidates = new SortedSet<int>();
            foreach (var biome in biomes)
            {
                var featuresInBiome = biome.Generation.Features;
                if (stepIndex >= featuresInBiome.Count) continue;
                foreach (var holder in featuresInBiome[stepIndex])
                {
                    if (!holder.IsBound()) continue;
                    var globalIndex = stepData.IndexOf(holder.Value);
                    if (globalIndex >= 0) candidates.Add(globalIndex);
                }
            }
            foreach (var globalIndex in candidates)
            {
                if (stepData.Features[globalIndex] is not GamePlacedFeature placed) continue;
                WorldgenRandom.SetFeatureSeed(random, decorationSeed, globalIndex, stepIndex);
                placed.PlaceWithBiomeCheck(region, this, random, origin);
            }
        }
    }

    //GroupStructuresByStep 按装饰步骤归类已装载的结构注册名 对应原版 structuresByStep
    //注册表冻结后内容不变 每次装饰重建一次与逐区块重新分组的原版行为一致
    private static Dictionary<int, List<Identifier>> GroupStructuresByStep()
    {
        var result = new Dictionary<int, List<Identifier>>();
        foreach (var holder in BuiltInRegistries.STRUCTURE.ListElements())
        {
            if (holder.Value is not GameStructure structure) continue;
            var step = (int)structure.Settings.Step;
            if (!result.TryGetValue(step, out var list)) result[step] = list = new List<Identifier>();
            list.Add(structure.Id);
        }
        return result;
    }

    //GetBaseHeight 旧版简化签名兼容测试与简单调用
    //内部委托抽象 GetBaseHeight(int,int,int,LevelHeightAccessor,RandomSource) 用 type=0 与占位 accessor
    public int GetBaseHeight(int x, int z)
    {
        Log.Debug($"GetBaseHeight entry x={x} z={z}");
        if (HeightNoise is null)
        {
            Log.Debug("GetBaseHeight exit result=0, HeightNoise is empty");
            return 0;
        }
        var value = HeightNoise.GetValue(x, 0, z);
        var result = (int)Math.Round(value * 32 + 64);
        Log.Debug($"GetBaseHeight exit result={result}");
        return result;
    }

    //GetBiome 委托 BiomeSource 查询生物群系
    public Biome GetBiome(int x, int y, int z)
        => BiomeSource.GetBiome(x, y, z);

    //GlobalFluidPicker 全局流体选择器内部实现对应原版 createFluidPicker 的 lambda
    //y < min(-54, seaLevel) 返回 lavaStatus 否则返回 seaStatus
    private sealed class GlobalFluidPicker : Aquifer.FluidPicker
    {
        private readonly FluidStatus _lavaStatus;
        private readonly FluidStatus _seaStatus;
        private readonly FluidStatus _emptyStatus;
        private readonly int _seaLevel;

        public GlobalFluidPicker(FluidStatus lava, FluidStatus sea, FluidStatus empty, int seaLevel)
        {
            _lavaStatus = lava;
            _seaStatus = sea;
            _emptyStatus = empty;
            _seaLevel = seaLevel;
        }

        public FluidStatus ComputeFluid(int blockX, int blockY, int blockZ)
        {
            if (blockY < Math.Min(-54, _seaLevel))
                return _lavaStatus;
            return _seaStatus;
        }
    }
}
