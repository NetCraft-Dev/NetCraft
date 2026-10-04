using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//SimpleServerLevel 服务端关卡基线实现对应原版 ServerLevel 的最小可用版本
//持有 in-memory ChunkAccess 字典按 ChunkPos.Pack 索引供 WorldGenRegion 与测试场景使用
//去掉 sealed 允许 PersistentServerLevel 继承复用 in-memory 缓存
public class SimpleServerLevel : ServerLevel
{
    private readonly Dictionary<long, ChunkAccess> _chunks = new();
    private readonly Identifier _dimension;
    private readonly int _dataVersion;
    private readonly RegistryAccess _registryAccess;

    public SimpleServerLevel(Identifier? dimension = null, int dataVersion = 0, RegistryAccess? registryAccess = null)
    {
        _dimension = dimension ?? Identifier.WithDefaultNamespace("overworld");
        _dataVersion = dataVersion;
        _registryAccess = registryAccess ?? RegistryAccess.Empty;
    }

    public override Identifier Dimension => _dimension;
    public override int DataVersion => _dataVersion;
    public override RegistryAccess RegistryAccess => _registryAccess;

    //AddChunk 加入区块到 in-memory 字典
    public void AddChunk(ChunkAccess chunk)
        => _chunks[ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z)] = chunk;

    //GetChunk 按 ChunkPos 查找区块未加载返回 null
    public override ChunkAccess? GetChunk(ChunkPos pos)
        => _chunks.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var chunk) ? chunk : null;

    //GetChunk 按 chunkX/chunkZ 查找区块
    public ChunkAccess? GetChunk(int chunkX, int chunkZ)
        => _chunks.TryGetValue(ChunkPos.Pack(chunkX, chunkZ), out var chunk) ? chunk : null;

    //RemoveChunk 把区块移出 in-memory 字典返回是否存在过
    //区块卸载时用 不移除的话 GetChunk 还会命中已卸载的旧对象 卸载等于没做
    public bool RemoveChunk(ChunkPos pos) => _chunks.Remove(ChunkPos.Pack(pos.X, pos.Z));
}
