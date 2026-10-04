using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using HeightmapRegistry = NetCraft.Registry.Heightmap;

namespace NetCraft.Storage;

//ChunkAccess 区块访问抽象基类对应原版 net.minecraft.world.level.chunk.ChunkAccess
//持有区块位置/高度访问/区块状态/高度图等基础字段
//子类 LevelChunk/ProtoChunk 按需扩展具体字段
public abstract class ChunkAccess : LevelHeightAccessor
{
    //Heightmap 实例缓存对应原版 heightmaps 字段
    //Heightmaps 抽象属性持有 long[] 序列化数据此缓存持有可变实例避免每次重建丢失更新
    private Dictionary<HeightmapRegistry.Types, LevelGen.Heightmap>? _heightmapCache;

    //Pos 区块位置
    public abstract ChunkPos Pos { get; }

    //MinSectionY 最低区段 Y 对应原版 getMinSection
    public abstract int MinSectionY { get; }

    //SectionsCount 区段数量对应原版 getSectionsCount
    public abstract int SectionsCount { get; }

    //MaxSectionY 由 MinSectionY+SectionsCount-1 推导对应原版 getMaxSection
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //ChunkStatus 区块状态
    public abstract ChunkStatus ChunkStatus { get; }

    //Heightmaps 高度图集合对应原版 getHeightmaps
    public abstract IDictionary<HeightmapRegistry.Types, long[]> Heightmaps { get; }

    //PostProcessingSections 需要后处理的位置按区段分组 每项是压成 short 的区段内局部坐标
    //对应原版 ChunkAccess.postProcessing 雕刻挖穿流体后要把位置登记进来
    public virtual List<short>?[] PostProcessingSections => Array.Empty<List<short>?>();

    //MarkPosForPostProcessing 登记一个需要后处理的方块位置对应原版 markPosForPostProcessing
    public virtual void MarkPosForPostProcessing(int worldX, int worldY, int worldZ) { }

    //BlockTicks 该区块的方块调度刻容器 对应原版 LevelChunk.blockTicks
    //生成期与运行期共用一套容器 原版生成期另有 ProtoChunkTicks 会把延迟丢成 0 这里先不区分
    public Ticks.LevelChunkTicks<NetCraft.Registry.Block> BlockTicks { get; } = new();

    //FluidTicks 该区块的流体调度刻容器
    public Ticks.LevelChunkTicks<NetCraft.Registry.Fluid> FluidTicks { get; } = new();

    //SetBlockState 按世界坐标写方块对应原版 setBlockState 越界区段直接丢弃
    public virtual void SetBlockState(int worldX, int worldY, int worldZ, BlockState state)
    {
        var section = GetSection(worldY >> 4);
        section?.SetBlockState(worldX & 15, worldY & 15, worldZ & 15, state);
    }

    //GetSection 按区段 Y 获取区段数据越界返回 null 对应原版 getSection
    public abstract LevelChunkSection? GetSection(int sectionY);

    //GetOrCreateHeightmapForType 按类型创建或获取高度图实例对应原版 getOrCreateHeightmap
    //首次调用从 Heightmaps long[] 重建实例并缓存后续调用返回同一实例
    public virtual LevelGen.Heightmap GetOrCreateHeightmapForType(HeightmapRegistry.Types type)
    {
        _heightmapCache ??= new Dictionary<HeightmapRegistry.Types, LevelGen.Heightmap>();
        if (_heightmapCache.TryGetValue(type, out var cached))
            return cached;
        var data = Heightmaps.TryGetValue(type, out var d) ? d : Array.Empty<long>();
        var instance = data.Length == 0
            ? new LevelGen.Heightmap(type, MinSectionY * 16, SectionsCount * 16)
            : LevelGen.Heightmap.FromData(type, MinSectionY * 16, SectionsCount * 16, data);
        _heightmapCache[type] = instance;
        return instance;
    }

    //GetHeight 对应原版 getHeight 按类型取列高度
    //该类型还没算过时先整体补算 对应原版 getHeight 里 heightmap 缺失走 primeHeightmaps 的分支
    public int GetHeight(HeightmapRegistry.Types type, int x, int z)
    {
        if (!Heightmaps.ContainsKey(type))
            LevelGen.Heightmap.PrimeHeightmaps(this, new[] { type });
        return GetOrCreateHeightmapForType(type).GetFirstAvailable(x, z);
    }

    //GetBlockState 按世界坐标读方块状态 区段越界返回空气 对应原版 ChunkAccess.getBlockState
    public virtual BlockState GetBlockState(int worldX, int worldY, int worldZ)
        => GetSection(worldY >> 4)?.GetBlockState(worldX & 15, worldY & 15, worldZ & 15) ?? default;

    //UpdateHeightmaps 方块变化后增量维护高度图 对应原版 LevelChunk.setBlockState 里的 heightmap.update
    //只维护已建实例的类型 没算过的类型下次 GetHeight 会整体补算
    //新方块不参与该高度图且原本占着该列最高点时 要向下重扫找新的遮挡物
    public virtual void UpdateHeightmaps(int worldX, int worldY, int worldZ, BlockState state)
    {
        if (_heightmapCache is null || _heightmapCache.Count == 0) return;
        var localX = worldX & 15;
        var localZ = worldZ & 15;
        foreach (var (type, map) in _heightmapCache)
        {
            //GetFirstAvailable 是最高遮挡方块的上方一格 这里要比的是方块本身的高度
            var firstAvailable = map.GetFirstAvailable(localX, localZ);
            var highest = firstAvailable == LevelGen.Heightmap.MinValue ? int.MinValue : firstAvailable - 1;
            //比原最高方块还低就动不到这一列
            if (worldY < highest) continue;
            if (LevelGen.Heightmap.IsOpaqueFor(type, state))
            {
                //只有高过原最高方块才需要抬高
                if (worldY <= highest) continue;
                map.SetHeight(localX, localZ, worldY + 1);
            }
            else
            {
                //正好把原最高方块换成不遮挡的 才往下重扫找新的遮挡物
                if (highest != worldY) continue;
                var found = int.MinValue;
                for (var y = worldY - 1; y >= MinSectionY * 16; y--)
                {
                    if (!LevelGen.Heightmap.IsOpaqueFor(type, GetBlockState(worldX, y, worldZ))) continue;
                    found = y;
                    break;
                }
                map.SetHeight(localX, localZ, found == int.MinValue ? int.MinValue : found + 1);
            }
            Heightmaps[type] = map.GetData();
        }
    }

    //SetBiome 按世界坐标写入生物群系对应原版 setBiome
    //世界坐标转 sectionY 与 quart 局部坐标委托 section.SetBiome
    public virtual void SetBiome(int worldX, int worldY, int worldZ, Holder<Biome> biome)
    {
        var section = GetSection(worldY >> 4);
        if (section is null) return;
        section.SetBiome((worldX >> 2) & 3, (worldY >> 2) & 3, (worldZ >> 2) & 3, biome);
    }

    //GetNoiseBiome 按 quart 世界坐标查询生物群系对应原版 getNoiseBiome
    public virtual Holder<Biome> GetNoiseBiome(int quartX, int quartY, int quartZ)
    {
        var section = GetSection((quartY >> 2) + MinSectionY);
        return section is null
            ? Holder<Biome>.Direct(EmptyBiome)
            : section.GetNoiseBiome(quartX & 3, quartY & 3, quartZ & 3);
    }

    //EmptyBiome 区段越界时返回的默认 biome 占位避免 null
    private static readonly Biome EmptyBiome = new EmptyBiomeImpl();
    private sealed class EmptyBiomeImpl : Biome
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("plains");
    }
}
