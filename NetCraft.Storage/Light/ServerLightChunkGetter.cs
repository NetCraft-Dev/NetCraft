using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//ServerLightChunkGetter, server-side light chunk provider, maps to vanilla ServerChunkCache in its LightChunkGetter role
//Looks up loaded chunks by chunk pos and turns them into light views; returns null when not loaded, which the engine treats as fully opaque
//The lookup must be read-only; triggering a load would recursively generate along the neighbor chain and overflow the stack
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

    //LightUpdateCallback, called for affected sections after each propagation round of the light engine; the chunk cache uses it to send incremental light packets
    public Action<LightLayer, SectionPos>? LightUpdateCallback { get; set; }

    //onLightUpdate forwards the light change notification, no longer letting the default no-op drop it
    public void OnLightUpdate(LightLayer layer, SectionPos pos) => LightUpdateCallback?.Invoke(layer, pos);

    //GetChunkForLighting caches views per chunk; building the sky light source heightmap is expensive and must not be redone each time
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

    public BlockGetter GetLevel() => this;

    public BlockState GetBlockState(int x, int y, int z)
        => _chunkLookup(x >> 4, z >> 4)?.GetSection(y >> 4)?.GetBlockState(x & 15, y & 15, z & 15) ?? default;

    //dropView clears the cached view on chunk unload or rebuild, avoiding a stale chunk reference
    public void DropView(int chunkX, int chunkZ) => _views.Remove(ChunkPos.Pack(chunkX, chunkZ));

    //UpdateSkyLightSources refreshes the sky light source heightmap of that column after a block change; skipped when the view is absent
    public void UpdateSkyLightSources(BlockPos pos)
    {
        if (_views.TryGetValue(ChunkPos.Pack(pos.X >> 4, pos.Z >> 4), out var view))
            view.UpdateSkyLightSources(pos.X & 15, pos.Y, pos.Z & 15);
    }
}
