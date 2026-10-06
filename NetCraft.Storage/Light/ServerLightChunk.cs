using NetCraft.Primitives;
using NetCraft.Registry.State;

namespace NetCraft.Storage.Light;

//ServerLightChunk, server-side light view, plays the role of both vanilla LevelChunk and LightChunk
//Exposes ChunkAccess the way the light engine needs; the sky light source column heightmap is built lazily from chunk content
public sealed class ServerLightChunk : LightChunk
{
    private readonly ChunkAccess _chunk;
    private ChunkSkyLightSources? _skySources;

    public ServerLightChunk(ChunkAccess chunk) => _chunk = chunk;

    //Height range is forwarded directly to the underlying chunk
    public int MinSectionY => _chunk.MinSectionY;
    public int MaxSectionY => _chunk.MaxSectionY;
    public int SectionsCount => _chunk.SectionsCount;

    public BlockState GetBlockState(int x, int y, int z)
    {
        var section = _chunk.GetSection(y >> 4);
        return section?.GetBlockState(x & 15, y & 15, z & 15) ?? default;
    }

    //getSkyLightSources builds the sky light source column from chunk content on first access
    public ChunkSkyLightSources GetSkyLightSources() => _skySources ??= BuildSkySources();

    //UpdateSkyLightSources refreshes the source height of that column after a block change; nothing to do before the heightmap is built
    public void UpdateSkyLightSources(int x, int y, int z) => _skySources?.Update(this, x, y, z);

    private ChunkSkyLightSources BuildSkySources()
    {
        var sources = new ChunkSkyLightSources(_chunk);
        sources.FillFrom(_chunk);
        return sources;
    }

    //findBlockLightSources walks all sections of the chunk and picks out light-emitting blocks
    public void FindBlockLightSources(Action<BlockPos, BlockState> consumer)
    {
        var minX = _chunk.Pos.X * 16;
        var minZ = _chunk.Pos.Z * 16;
        for (var sectionY = _chunk.MinSectionY; sectionY <= _chunk.MaxSectionY; sectionY++)
        {
            var section = _chunk.GetSection(sectionY);
            if (section is null || section.HasOnlyAir()) continue;
            var minY = sectionY * 16;
            for (var y = 0; y < 16; y++)
            for (var z = 0; z < 16; z++)
            for (var x = 0; x < 16; x++)
            {
                var state = section.GetBlockState(x, y, z);
                if (state.GetLightEmission() > 0)
                    consumer(new BlockPos(minX + x, minY + y, minZ + z), state);
            }
        }
    }
}
