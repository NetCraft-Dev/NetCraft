using NetCraft.Registry;

namespace NetCraft.Storage;

//ChunkLevel, chunk ticket level constants and conversions, maps to vanilla net.minecraft.server.level.ChunkLevel
//Lower level numbers are closer to the ticket source: 33 loads only to FULL, 31 lets entities tick; above MaxLevel it is not loaded at all
//Ticket levels decay by +1 per neighbor step, so the level itself expresses "how far from the nearest ticket source"
public static class ChunkLevel
{
    //FullChunkLevel, the level loaded to FULL, maps to vanilla FULL_CHUNK_LEVEL
    public const int FullChunkLevel = 33;

    //BlockTickingLevel, the level at which blocks tick, maps to vanilla BLOCK_TICKING_LEVEL
    public const int BlockTickingLevel = 32;

    //EntityTickingLevel, the level at which entities tick, maps to vanilla ENTITY_TICKING_LEVEL
    public const int EntityTickingLevel = 31;

    //RadiusAroundFullChunk, the radius kept around FULL for generation dependencies, maps to vanilla RADIUS_AROUND_FULL_CHUNK
    //Its value is the accumulated dependency table length minus one, determined by the radius-8 requirement of the STRUCTURE_START entry
    public const int RadiusAroundFullChunk = 8;

    //MaxLevel, the maximum level for this dimension; above it is not loaded, maps to vanilla MAX_LEVEL
    public const int MaxLevel = FullChunkLevel + RadiusAroundFullChunk;

    //AccumulatedDependencies, the minimum status required at each distance from FULL, index is the distance, maps to the accumulated dependency table of the vanilla FULL step
    //Derived by the vanilla accumulation rule: the radius-8 requirement of STRUCTURE_START fills 0..8, each entry taking the weaker of itself and the parent table
    //At distance 8 only EMPTY remains, meaning the outermost neighbors of FULL need only reach EMPTY
    private static readonly ChunkStatus[] AccumulatedDependencies =
    {
        ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START,
        ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START,
        ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START, ChunkStatus.EMPTY,
    };

    //GenerationStatus, the highest status this level may generate to, maps to vanilla generationStatus
    //Returns null beyond the radius, meaning the chunk takes no part in generation
    public static ChunkStatus? GenerationStatus(int level)
        => GetStatusAroundFullChunk(level - FullChunkLevel, null);

    //GetStatusAroundFullChunk, the minimum status required at the given distance from FULL, maps to vanilla getStatusAroundFullChunk
    public static ChunkStatus? GetStatusAroundFullChunk(int distanceToFullChunk, ChunkStatus? defaultValue)
    {
        if (distanceToFullChunk > RadiusAroundFullChunk) return defaultValue;
        if (distanceToFullChunk <= 0) return ChunkStatus.FULL;
        return AccumulatedDependencies[distanceToFullChunk];
    }

    //ByStatus, the level for the given status, maps to vanilla byStatus
    //Only statuses that appear in the accumulated dependency table can be converted; vanilla throws out of bounds for others
    public static int ByStatus(ChunkStatus status)
        => status == ChunkStatus.FULL ? FullChunkLevel : FullChunkLevel + RadiusOf(status);

    //RadiusOf, the radius this status requires in the accumulated dependency table, maps to vanilla ChunkDependencies.getRadiusOf
    private static int RadiusOf(ChunkStatus status)
    {
        if (status == ChunkStatus.EMPTY) return RadiusAroundFullChunk;
        if (status == ChunkStatus.STRUCTURE_START) return RadiusAroundFullChunk - 1;
        throw new ArgumentException($"Status {status} is not in the accumulated dependency range", nameof(status));
    }

    //FullStatus, the load tier for the given level, maps to vanilla fullStatus
    public static FullChunkStatus FullStatus(int level)
    {
        if (level <= EntityTickingLevel) return FullChunkStatus.EntityTicking;
        if (level <= BlockTickingLevel) return FullChunkStatus.BlockTicking;
        if (level <= FullChunkLevel) return FullChunkStatus.Full;
        return FullChunkStatus.Inaccessible;
    }

    //ByStatus, the level for the load tier, maps to vanilla byStatus(FullChunkStatus)
    public static int ByStatus(FullChunkStatus status)
    {
        if (status == FullChunkStatus.Inaccessible) return MaxLevel;
        if (status == FullChunkStatus.Full) return FullChunkLevel;
        if (status == FullChunkStatus.BlockTicking) return BlockTickingLevel;
        return EntityTickingLevel;
    }

    //IsEntityTicking, whether the level can tick entities, maps to vanilla isEntityTicking
    public static bool IsEntityTicking(int level) => level <= EntityTickingLevel;

    //IsBlockTicking, whether the level can tick blocks, maps to vanilla isBlockTicking
    public static bool IsBlockTicking(int level) => level <= BlockTickingLevel;

    //IsLoaded, whether the level is within the load range, maps to vanilla isLoaded
    public static bool IsLoaded(int level) => level <= MaxLevel;
}
