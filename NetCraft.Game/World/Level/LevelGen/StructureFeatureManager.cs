using System.Collections.Concurrent;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureFeatureManager 结构管理器 对应原版 net.minecraft.world.level.StructureManager
//持本世界已装配的结构结果与跨区块引用
//整个维度共享一个实例 结构按区块存进来 装饰阶段才能看到邻块已装配的结构
//生成期由多个区块并行推进 表用并发字典 查询走无锁读 对应原版 ConcurrentHashMap
//原来整张表挂一把锁 装饰阶段每区块要按结构逐个查邻域 十几个生成线程全被串起来
public sealed class StructureFeatureManager
{
    //一个区块可以同时有多个结构的装配结果 键是区块压缩坐标
    //内层只被持有它的线程写 读方并发安全
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Identifier, StructureStart>> _starts = new();
    //每个 chunk 可能被多个跨 chunk 结构的包围盒覆盖故用 List
    //List 不并发写 追加与拷贝都拿 List 自身当锁 按键分开互不干扰
    private readonly ConcurrentDictionary<long, List<StructureReference>> _references = new();

    private readonly ChunkGenerator? _generator;
    private readonly StructurePlacementRegistry? _registry;

    //无参构造用于不生成结构的场景 此时 CreateStarts 直接返回 0
    public StructureFeatureManager() { }

    public StructureFeatureManager(ChunkGenerator generator, StructurePlacementRegistry registry)
    {
        _generator = generator;
        _registry = registry;
    }

    //AllStarts 全部已装配结果快照 供诊断与引用扫描
    //返回拷贝而不是活视图 遍历期间别的线程还会往表里写
    public IEnumerable<StructureStart> AllStarts
    {
        get
        {
            var snapshot = new List<StructureStart>();
            foreach (var perStructure in _starts.Values)
                snapshot.AddRange(perStructure.Values);
            return snapshot;
        }
    }

    //HasStructureReferences 查询区块是否有结构引用
    public bool HasStructureReferences(ChunkPos pos)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        if (!_references.TryGetValue(key, out var list)) return false;
        lock (list) return list.Count > 0;
    }

    //GetReferences 返回区块的全部结构引用 未命中返回空列表
    public IReadOnlyList<StructureReference> GetReferences(ChunkPos pos)
    {
        if (!_references.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var list))
            return Array.Empty<StructureReference>();
        lock (list) return new List<StructureReference>(list);
    }

    //HasStructureStartsForChunk 查询区块是否有任意结构装配结果
    public bool HasStructureStartsForChunk(ChunkAccess chunk)
        => _starts.TryGetValue(ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z), out var perStructure)
            && !perStructure.IsEmpty;

    //GetStructureStarts 取该区块的全部装配结果 未命中返回空
    public IReadOnlyCollection<StructureStart> GetStructureStarts(ChunkPos pos)
        => _starts.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var perStructure)
            ? new List<StructureStart>(perStructure.Values)
            : Array.Empty<StructureStart>();

    //ShouldGenerateStructures 本世界是否生成结构 对应原版 StructureManager.shouldGenerateStructures
    //由服务端配置 generate-structures 注入 关掉后 CreateStarts 直接返回 0 连结构模板都不会加载
    public bool ShouldGenerateStructures { get; set; } = true;

    //SearchRadius 结构最大水平半径 128 格即 8 区块
    //对应原版 JigsawStructure.MAX_TOTAL_STRUCTURE_RANGE 换算成区块数
    private const int SearchRadius = 8;

    //StartsForStructure 取包围盒覆盖目标区块的指定结构 对应原版 StructureManager.startsForStructure
    //结果按邻域扫描 同一结构跨越多区块时每个区块都会拿到它
    public IEnumerable<StructureStart> StartsForStructure(ChunkPos chunk, Identifier structureId)
    {
        var result = new List<StructureStart>();
        var target = BoundingBoxInt.FromChunkPos(chunk);
        for (var dx = -SearchRadius; dx <= SearchRadius; dx++)
        for (var dz = -SearchRadius; dz <= SearchRadius; dz++)
        {
            if (!_starts.TryGetValue(ChunkPos.Pack(chunk.X + dx, chunk.Z + dz), out var perStructure))
                continue;
            if (!perStructure.TryGetValue(structureId, out var start)) continue;
            if (!start.BoundingBox.Intersects(target)) continue;
            result.Add(start);
        }
        return result;
    }

    //StartsForStructure 取包围盒覆盖目标区块且满足条件的装配结果 对应原版 startsForStructure(pos, predicate)
    //地形适配要按 terrain_adaptation 过滤 只要需要改编地形的那些结构
    public IReadOnlyList<StructureStart> StartsForStructure(ChunkPos chunk, Func<StructureStart, bool> predicate)
    {
        var result = new List<StructureStart>();
        var target = BoundingBoxInt.FromChunkPos(chunk);
        for (var dx = -SearchRadius; dx <= SearchRadius; dx++)
        for (var dz = -SearchRadius; dz <= SearchRadius; dz++)
        {
            if (!_starts.TryGetValue(ChunkPos.Pack(chunk.X + dx, chunk.Z + dz), out var perStructure))
                continue;
            foreach (var start in perStructure.Values)
            {
                if (!start.BoundingBox.Intersects(target)) continue;
                if (!predicate(start)) continue;
                result.Add(start);
            }
        }
        return result;
    }

    //AddStructureStart 记录某个结构的装配结果 同结构重复装配时后者覆盖
    public void AddStructureStart(ChunkPos pos, StructureStart start)
    {
        var key = ChunkPos.Pack(pos.X, pos.Z);
        //内层字典的创建与写入只由持有该区块的生成线程做 读方并发安全
        var perStructure = _starts.GetOrAdd(key, static _ => new ConcurrentDictionary<Identifier, StructureStart>());
        perStructure[start.StructureId] = start;
    }

    //AddStructureReference 记录一条跨区块引用 同结构同源区块只记一次
    //原版引用表是 Map<Structure, Set<Long>> 重复追加会让落盘数据无谓膨胀
    public void AddStructureReference(ChunkPos pos, StructureReference reference)
    {
        Log.Debug($"AddStructureReference entry pos={pos} reference={reference.StructureId}");
        var key = ChunkPos.Pack(pos.X, pos.Z);
        var list = _references.GetOrAdd(key, static _ => new List<StructureReference>());
        //同一个 List 可能被多个线程追加 拿它自身当锁 不同区块各锁各的互不影响
        lock (list)
        {
            if (!list.Contains(reference)) list.Add(reference);
        }
        //Log.Debug("AddStructureReference 出口");
    }

    //CreateStarts 装配当前区块命中的结构 对应原版 ChunkGenerator.createStructures 的集合循环
    //每个集合按权重抽一个结构 抽中但装配无效就把该项剔除重抽 直到成功或没有候选项
    //返回装配成功的结构数
    public int CreateStarts(ChunkAccess chunk)
    {
        if (_generator is null || _registry is null) return 0;
        //关掉结构生成时直接跳过 不装配也不加载任何结构模板
        if (!ShouldGenerateStructures) return 0;
        var sets = _registry.GetSetsForChunk(chunk.Pos);
        var count = 0;
        foreach (var set in sets)
        {
            //该区块已经有本集合的结构时不重复生成 原版靠 hasStructureStart 判断
            if (HasStartForSet(chunk.Pos, set)) continue;
            count += PickAndGenerate(set, chunk);
        }
        return count;
    }

    //PickAndGenerate 按权重抽结构并装配 对应原版 createStructures 里的抽取循环
    //随机源与种子派生必须逐字对齐原版 否则结构分布会与原版不同
    private int PickAndGenerate(StructureSet set, ChunkAccess chunk)
    {
        var options = new List<StructureSelectionEntry>(set.Structures);
        var total = set.WeightTotal;
        var random = new LegacyRandomSource(0L);
        WorldgenRandom.SetLargeFeatureSeed(random, _registry!.Seed, chunk.Pos.X, chunk.Pos.Z);
        while (options.Count > 0)
        {
            var choice = total > 0 ? random.NextInt(total) : 0;
            StructureSelectionEntry? picked = null;
            foreach (var entry in options)
            {
                choice -= entry.Weight;
                if (choice < 0)
                {
                    picked = entry;
                    break;
                }
            }
            if (picked is null) break;
            if (TryGenerate(picked, chunk)) return 1;
            //装配无效 剔除该项后重抽 权重总和同步扣减
            options.Remove(picked);
            total -= picked.Weight;
        }
        return 0;
    }

    //TryGenerate 调结构装配并登记结果
    private bool TryGenerate(StructureSelectionEntry entry, ChunkAccess chunk)
    {
        if (!entry.Structure.IsBound() || entry.Structure.Value is not Structure structure) return false;
        var context = new GenerationContext(_generator!, _registry!.Seed, chunk.Pos, chunk);
        //声明了群系的结构才做过滤 程序化与测试结构不声明群系按放行处理
        if (structure.Settings.Biomes.Size > 0)
            context.ValidBiome = pos => context.IsBiomeAllowed(structure, pos);
        var start = structure.Generate(context);
        if (!start.IsValid) return false;
        AddStructureStart(chunk.Pos, start);
        Log.Debug($"Structure assembled {structure.Id} chunk={chunk.Pos} pieces={start.Pieces.Count}");
        return true;
    }

    //HasStartForSet 该区块是否已有本集合中任一结构的装配结果
    private bool HasStartForSet(ChunkPos pos, StructureSet set)
    {
        if (!_starts.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var perStructure)) return false;
        foreach (var entry in set.Structures)
        {
            if (!entry.Structure.IsBound()) continue;
            if (entry.Structure.Value is Structure structure && perStructure.ContainsKey(structure.Id))
                return true;
        }
        return false;
    }

    //CollectReferences 扫描目标 chunk 周围 radius 半径内邻居的装配结果
    //包围盒与目标 chunk 相交的记为引用 对应原版 createReferences
    //原版半径是 8 覆盖 17x17 个区块
    public int CollectReferences(ChunkPos pos, int radius = 8)
    {
        var targetBox = BoundingBoxInt.FromChunkPos(pos);
        //无锁扫邻域 命中即写引用表 对应原版 createReferences 的边扫边加
        //表里只会有装配成功的结构 TryGenerate 已滤掉无效结果
        var count = 0;
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dz = -radius; dz <= radius; dz++)
            {
                var neighborPos = new ChunkPos(pos.X + dx, pos.Z + dz);
                if (!_starts.TryGetValue(ChunkPos.Pack(neighborPos.X, neighborPos.Z), out var perStructure))
                    continue;
                foreach (var start in perStructure.Values)
                {
                    if (!start.BoundingBox.Intersects(targetBox)) continue;
                    AddStructureReference(pos, new StructureReference(start.StructureId, neighborPos));
                    count++;
                }
            }
        }
        return count;
    }
}

//StructureReference 结构引用 记录某个区块被哪个结构覆盖以及结构所在区块
//record 按值比较 引用表要按它去重
public sealed record StructureReference(Identifier StructureId, ChunkPos TargetChunk);
