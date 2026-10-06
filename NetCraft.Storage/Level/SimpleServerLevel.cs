using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Storage;

//SimpleServerLevel, minimal usable server level baseline, maps to vanilla ServerLevel
//Holds an in-memory ChunkAccess dictionary indexed by ChunkPos.Pack, used by WorldGenRegion and test scenarios
//Not sealed, so PersistentServerLevel can inherit and reuse the in-memory cache
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

    //AddChunk adds a chunk to the in-memory dictionary
    public void AddChunk(ChunkAccess chunk)
        => _chunks[ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z)] = chunk;

    //GetChunk looks up a chunk by ChunkPos; returns null when not loaded
    public override ChunkAccess? GetChunk(ChunkPos pos)
        => _chunks.TryGetValue(ChunkPos.Pack(pos.X, pos.Z), out var chunk) ? chunk : null;

    //GetChunk looks up a chunk by chunkX/chunkZ
    public ChunkAccess? GetChunk(int chunkX, int chunkZ)
        => _chunks.TryGetValue(ChunkPos.Pack(chunkX, chunkZ), out var chunk) ? chunk : null;

    //RemoveChunk removes a chunk from the in-memory dictionary and returns whether it existed
    //Used on chunk unload; without removal GetChunk would still hit the old unloaded object, making the unload a no-op
    public bool RemoveChunk(ChunkPos pos) => _chunks.Remove(ChunkPos.Pack(pos.X, pos.Z));
}
