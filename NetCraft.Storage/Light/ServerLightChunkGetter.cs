using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//ServerLightChunkGetter 服务端光照区块提供者对应原版 ServerChunkCache 作为 LightChunkGetter 的角色
//按区块坐标查已加载区块转成光照视图 未加载返回 null 由引擎按完全不透明处理
//查询必须是只读的 触发加载会沿邻居链递归生成导致栈溢出
public sealed class ServerLightChunkGetter : LightChunkGetter, BlockGetter
{
    private readonly Func<int, int, ChunkAccess?> _chunkLookup;
    private readonly Dictionary<long, ServerLightChunk> _views = new();

    public ServerLightChunkGetter(Func<int, int, ChunkAccess?> chunkLookup, int minSectionY, int sectionsCount)
    {
        _chunkLookup = chunkLookup;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
        MaxSectionY = minSectionY + sectionsCount - 1;
    }

    public int MinSectionY { get; }
    public int MaxSectionY { get; }
    public int SectionsCount { get; }

    //LightUpdateCallback 光照引擎每轮传播后回调受影响区段 由区块缓存接管用于下发增量光照包
    public Action<LightLayer, SectionPos>? LightUpdateCallback { get; set; }

    //onLightUpdate 转发光照变化通知 不再让默认空实现把通知丢掉
    public void OnLightUpdate(LightLayer layer, SectionPos pos) => LightUpdateCallback?.Invoke(layer, pos);

    //getChunkForLighting 视图按区块缓存 天光光源高度图构建代价高不能每次重建
    public LightChunk? GetChunkForLighting(int chunkX, int chunkZ)
    {
        var key = ChunkPos.Pack(chunkX, chunkZ);
        if (_views.TryGetValue(key, out var cached)) return cached;
        var chunk = _chunkLookup(chunkX, chunkZ);
        if (chunk is null) return null;
        var view = new ServerLightChunk(chunk);
        _views[key] = view;
        return view;
    }

    //Track 登记正在计算光照的区块 此时区块尚未并入已加载缓存靠光照取块器查不到自身
    public void Track(ChunkAccess chunk)
        => _views[ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z)] = new ServerLightChunk(chunk);

    public BlockGetter GetLevel() => this;

    public BlockState GetBlockState(int x, int y, int z)
        => _chunkLookup(x >> 4, z >> 4)?.GetSection(y >> 4)?.GetBlockState(x & 15, y & 15, z & 15) ?? default;

    //dropView 区块卸载或重建时清掉缓存视图避免持有过期区块
    public void DropView(int chunkX, int chunkZ) => _views.Remove(ChunkPos.Pack(chunkX, chunkZ));

    //UpdateSkyLightSources 方块变更后刷新该列的天光光源高度图 视图不存在时跳过
    public void UpdateSkyLightSources(BlockPos pos)
    {
        if (_views.TryGetValue(ChunkPos.Pack(pos.X >> 4, pos.Z >> 4), out var view))
            view.UpdateSkyLightSources(pos.X & 15, pos.Y, pos.Z & 15);
    }
}
