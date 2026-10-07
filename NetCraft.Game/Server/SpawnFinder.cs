using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Server;

//SpawnFinder new-world spawn search, maps to the candidate chunk scan of vanilla MinecraftServer.setInitialSpawn
//From the suggested landing point it walks chunks in vanilla spiral order looking for a standable surface, stopping on the first hit
public static class SpawnFinder
{
    //CandidateRadius candidate chunk radius, maps to ±5 of vanilla Mth.square(11)
    private const int CandidateRadius = 5;

    //Find finds a safe spawn point; returns null when none, leaving the caller to keep the original value
    //suggestion is the suggested landing point; vanilla gives it by noise sampling, this takes the current spawn
    public static BlockPos? Find(PersistentServerLevel level, BlockPos suggestion, int minY)
    {
        var centerX = suggestion.X >> 4;
        var centerZ = suggestion.Z >> 4;
        var offsetX = 0;
        var offsetZ = 0;
        var stepX = 0;
        var stepZ = -1;
        for (var i = 0; i < (CandidateRadius * 2 + 1) * (CandidateRadius * 2 + 1); i++)
        {
            if (Math.Abs(offsetX) <= CandidateRadius && Math.Abs(offsetZ) <= CandidateRadius
                && FindInChunk(level, new ChunkPos(centerX + offsetX, centerZ + offsetZ), minY) is { } found)
                return found;
            //Vanilla spiral stepping turning at corners; the order participates in the selection result and must not change
            if (offsetX == offsetZ || (offsetX < 0 && offsetX == -offsetZ)
                || (offsetX > 0 && offsetX == 1 - offsetZ))
                (stepX, stepZ) = (-stepZ, stepX);
            offsetX += stepX;
            offsetZ += stepZ;
        }
        return null;
    }

    //FindInChunk scans the chunk column by column for the first safe landing point, maps to vanilla PlayerSpawnFinder.getSpawnPosInChunk
    private static BlockPos? FindInChunk(PersistentServerLevel level, ChunkPos pos, int minY)
    {
        if (level.GetChunkSync(pos) is not { } chunk) return null;
        var baseX = pos.X << 4;
        var baseZ = pos.Z << 4;
        for (var localX = 0; localX < 16; localX++)
            for (var localZ = 0; localZ < 16; localZ++)
                if (FindColumn(level, chunk, baseX + localX, baseZ + localZ, minY) is { } found)
                    return found;
        return null;
    }

    //FindColumn finds a standable surface in one column, maps to vanilla PlayerSpawnFinder.getLevelRespawnPos
    //MOTION_BLOCKING gives the standable surface; WORLD_SURFACE and OCEAN_FLOOR are used to exclude water
    private static BlockPos? FindColumn(PersistentServerLevel level, ChunkAccess chunk, int x, int z, int minY)
    {
        var localX = x & 15;
        var localZ = z & 15;
        var topY = chunk.GetHeight(Heightmap.Types.MotionBlocking, localX, localZ);
        if (topY < minY) return null;
        var surface = chunk.GetHeight(Heightmap.Types.WorldSurface, localX, localZ);
        if (surface <= topY && surface > chunk.GetHeight(Heightmap.Types.OceanFloor, localX, localZ)) return null;
        CollisionGetter collision = new LevelCollisionGetter(level, level.MinSectionY, level.SectionsCount);
        for (var y = topY + 1; y >= minY; y--)
        {
            //Read the chunk directly; the spawn search runs before the main loop and the level cache may not be merged yet
            if (chunk.GetSection(y >> 4)?.GetBlockState(localX, y & 15, localZ) is not { } state) continue;
            //A fluid on top means this column is water and is given up directly
            if (!state.FluidState.IsEmpty) break;
            var pos = new BlockPos(x, y, z);
            if (Block.IsFaceFull(CollisionContext.Empty.GetCollisionShape(state, collision, pos), Direction.Up))
                return new BlockPos(x, y + 1, z);
        }
        return null;
    }
}
