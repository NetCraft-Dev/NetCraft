using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//WorldGenRegion 世界生成区域访问接口对应原版 net.minecraft.server.level.WorldGenRegion
//持有 ServerLevel 引用 GetChunk 委托到 ServerLevel 实现真实接入
//无 ServerLevel 时回退到 in-memory 字典兼容旧测试场景
//实现 LevelHeightAccessor 让放置上下文与高度提供者能直接用它解算 Y 区间
public sealed class WorldGenRegion : LevelHeightAccessor
{
    private readonly ServerLevel? _level;
    private readonly Dictionary<long, ChunkAccess> _fallbackChunks = new();
    public int MinSectionY { get; }
    public int SectionsCount { get; }

    //MaxSectionY 最高区段 Y 由区间起点与数量推出
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //WorldGenRegion 接入 ServerLevel 真实路径区块查询委托到 ServerLevel.GetChunk
    public WorldGenRegion(ServerLevel level, int minSectionY, int sectionsCount)
    {
        _level = level;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //WorldGenRegion 旧构造无 ServerLevel 时走 in-memory 字典占位
    //兼容阶段 D 测试场景真实场景应传 ServerLevel
    public WorldGenRegion(int minSectionY, int sectionsCount)
    {
        _level = null;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //Level 持有的 ServerLevel 引用未接入时返回 null
    public ServerLevel? Level => _level;

    //WriteRadius 允许写入的区块半径 负数表示不限制
    //装饰阶段按中心区块限定 3x3 越界写入静默失败 对应原版 WorldGenRegion.writeRadius
    public int WriteRadius { get; set; } = -1;

    //CenterChunk 写入半径的基准区块 只有 WriteRadius 非负时参与判定
    public ChunkPos CenterChunk { get; set; }

    //Seed 本区域所属世界的种子 装饰阶段的种子派生以它为基准 对应原版 WorldGenLevel.getSeed
    public long Seed { get; set; }

    //EnsureCanWrite 判定目标区块是否落在可写半径内 对应原版 ensureCanWrite
    //越界返回假由调用方静默跳过 不是错误
    public bool EnsureCanWrite(int chunkX, int chunkZ)
    {
        if (WriteRadius < 0) return true;
        return Math.Abs(CenterChunk.X - chunkX) <= WriteRadius
            && Math.Abs(CenterChunk.Z - chunkZ) <= WriteRadius;
    }

    //AddChunk 加入区块到 in-memory 字典仅 fallback 路径有效
    public void AddChunk(ChunkAccess chunk)
        => _fallbackChunks[ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z)] = chunk;

    //GetChunk 优先委托到 ServerLevel.GetChunk无 ServerLevel 时走 fallback 字典
    public ChunkAccess? GetChunk(int chunkX, int chunkZ)
    {
        if (_level is not null)
        {
            if (_level is SimpleServerLevel simple)
                return simple.GetChunk(chunkX, chunkZ);
            return _level.GetChunk(new ChunkPos(chunkX, chunkZ));
        }
        return _fallbackChunks.TryGetValue(ChunkPos.Pack(chunkX, chunkZ), out var chunk) ? chunk : null;
    }

    //GetBlockState 按世界坐标获取方块状态委托到对应区块的 section
    public BlockState GetBlockState(int worldX, int worldY, int worldZ)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;
        var chunk = GetChunk(chunkX, chunkZ);
        if (chunk is null) return default;
        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;
        var sectionY = worldY >> 4;
        var localY = worldY & 0xF;
        var section = chunk.GetSection(sectionY);
        return section?.GetBlockState(localX, localY, localZ) ?? default;
    }

    //SetBlockState 按世界坐标设置方块状态返回旧状态
    //委托到对应区块的 section若区块不存在或越界返回 default
    public BlockState SetBlockState(int worldX, int worldY, int worldZ, BlockState state)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;
        if (!EnsureCanWrite(chunkX, chunkZ)) return default;
        if (GetChunk(chunkX, chunkZ) is not ProtoChunk proto) return default;
        //越界高度直接丢弃 对应原版 WorldGenRegion.setBlock 的高度检查
        //特征放置会算到世界高度之外的 Y 不拦就在 GetOrCreateSection 抛异常把整块区块的生成掀掉
        if (worldY < proto.MinSectionY * 16 || worldY > proto.MaxSectionY * 16 + 15) return default;
        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;
        var sectionY = worldY >> 4;
        var localY = worldY & 0xF;
        return proto.SetBlockState(sectionY, localX, localY, localZ, state);
    }

    //GetHeight 取指定高度图在该列的高度对应原版 getHeight
    //高度图修饰器靠它把 xz 定位到 y 区块不在区域内时退回区间底面
    public int GetHeight(NetCraft.Registry.Heightmap.Types type, int worldX, int worldZ)
    {
        var chunk = GetChunk(worldX >> 4, worldZ >> 4);
        return chunk?.GetHeight(type, worldX & 0xF, worldZ & 0xF) ?? MinSectionY * 16;
    }
}
