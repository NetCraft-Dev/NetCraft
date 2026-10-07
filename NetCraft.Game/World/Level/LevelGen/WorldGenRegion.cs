using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.World.Level.LevelGen;

//WorldGenRegion world generation region access, maps to vanilla net.minecraft.server.level.WorldGenRegion
//Holds a ServerLevel reference; GetChunk delegates to ServerLevel for real wiring
//Falls back to an in-memory dictionary without a ServerLevel, keeping old test scenarios working
//Implements LevelHeightAccessor so placement contexts and height providers can resolve the Y range directly
public sealed class WorldGenRegion : LevelHeightAccessor
{
    private readonly ServerLevel? _level;
    private readonly Dictionary<long, ChunkAccess> _fallbackChunks = new();
    public int MinSectionY { get; }
    public int SectionsCount { get; }

    //MaxSectionY highest section Y, derived from the range start and count
    public int MaxSectionY => MinSectionY + SectionsCount - 1;

    //WorldGenRegion with a ServerLevel; the real path delegates chunk queries to ServerLevel.GetChunk
    public WorldGenRegion(ServerLevel level, int minSectionY, int sectionsCount)
    {
        _level = level;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //WorldGenRegion legacy constructor uses the in-memory dictionary placeholder without a ServerLevel
    //Kept for phase D test scenarios; real scenarios should pass a ServerLevel
    public WorldGenRegion(int minSectionY, int sectionsCount)
    {
        _level = null;
        MinSectionY = minSectionY;
        SectionsCount = sectionsCount;
    }

    //Level the held ServerLevel reference, null when not wired up
    public ServerLevel? Level => _level;

    //WriteRadius chunk radius writable, a negative value means unlimited
    //Decoration limits writes to a 3x3 around the centre chunk and out-of-range writes fail silently, maps to vanilla WorldGenRegion.writeRadius
    public int WriteRadius { get; set; } = -1;

    //CenterChunk base chunk of the write radius, only used when WriteRadius is non-negative
    public ChunkPos CenterChunk { get; set; }

    //Seed seed of the world this region belongs to; decoration derives its seeds from it, maps to vanilla WorldGenLevel.getSeed
    public long Seed { get; set; }

    //EnsureCanWrite whether the target chunk is inside the write radius, maps to vanilla ensureCanWrite
    //Out of range returns false and the caller skips silently; it is not an error
    public bool EnsureCanWrite(int chunkX, int chunkZ)
    {
        if (WriteRadius < 0) return true;
        return Math.Abs(CenterChunk.X - chunkX) <= WriteRadius
            && Math.Abs(CenterChunk.Z - chunkZ) <= WriteRadius;
    }

    //AddChunk adds a chunk to the in-memory dictionary, only effective on the fallback path
    public void AddChunk(ChunkAccess chunk)
        => _fallbackChunks[ChunkPos.Pack(chunk.Pos.X, chunk.Pos.Z)] = chunk;

    //GetChunk prefers ServerLevel.GetChunk, falling back to the fallback dictionary without a ServerLevel
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

    //GetBlockState gets a block state by world coordinate, delegating to the chunk's section
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

    //SetBlockState sets a block state by world coordinate and returns the old state
    //Delegates to the chunk's section; returns default if the chunk is missing or out of range
    public BlockState SetBlockState(int worldX, int worldY, int worldZ, BlockState state)
    {
        var chunkX = worldX >> 4;
        var chunkZ = worldZ >> 4;
        if (!EnsureCanWrite(chunkX, chunkZ)) return default;
        if (GetChunk(chunkX, chunkZ) is not ProtoChunk proto) return default;
        //Out-of-range heights are dropped, matching the height check in vanilla WorldGenRegion.setBlock
        //Feature placement can produce Y outside the world height; without this guard GetOrCreateSection throws and blows up the whole chunk
        if (worldY < proto.MinSectionY * 16 || worldY > proto.MaxSectionY * 16 + 15) return default;
        var localX = worldX & 0xF;
        var localZ = worldZ & 0xF;
        var sectionY = worldY >> 4;
        var localY = worldY & 0xF;
        return proto.SetBlockState(sectionY, localX, localY, localZ, state);
    }

    //GetHeight returns the height of the given heightmap at that column, maps to vanilla getHeight
    //The heightmap placement modifier uses it to turn xz into y; falls back to the range floor when the chunk is outside the region
    public int GetHeight(NetCraft.Registry.Heightmap.Types type, int worldX, int worldZ)
    {
        var chunk = GetChunk(worldX >> 4, worldZ >> 4);
        return chunk?.GetHeight(type, worldX & 0xF, worldZ & 0xF) ?? MinSectionY * 16;
    }
}
