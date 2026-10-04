using NetCraft.Registry;

namespace NetCraft.Storage;

//ChunkLevel 区块票等级常量与换算对应原版 net.minecraft.server.level.ChunkLevel
//等级数值越小越靠近票源: 33 只加载到 FULL 31 则实体可 tick 超过 MaxLevel 视为完全不加载
//票等级会向邻居逐格 +1 衰减 所以等级本身表达了"离最近的票源有多远"
public static class ChunkLevel
{
    //FullChunkLevel 加载到 FULL 的等级对应原版 FULL_CHUNK_LEVEL
    public const int FullChunkLevel = 33;

    //BlockTickingLevel 方块可 tick 的等级对应原版 BLOCK_TICKING_LEVEL
    public const int BlockTickingLevel = 32;

    //EntityTickingLevel 实体可 tick 的等级对应原版 ENTITY_TICKING_LEVEL
    public const int EntityTickingLevel = 31;

    //RadiusAroundFullChunk FULL 周围为生成依赖保留的半径对应原版 RADIUS_AROUND_FULL_CHUNK
    //取值等于下面累积依赖表长度减一 表里 STRUCTURE_START 那条半径 8 的 requirement 决定了它
    public const int RadiusAroundFullChunk = 8;

    //MaxLevel 本维度最大等级超过即不加载对应原版 MAX_LEVEL
    public const int MaxLevel = FullChunkLevel + RadiusAroundFullChunk;

    //AccumulatedDependencies FULL 各距离上要求的最低状态 索引即距离 对应原版 FULL step 的累积依赖表
    //按原版累积规则推出: STRUCTURE_START 半径 8 的 requirement 铺满 0..8 逐项与父表取更弱者
    //距离 8 处只剩 EMPTY 说明 FULL 最外圈邻居只要到 EMPTY 即可
    private static readonly ChunkStatus[] AccumulatedDependencies =
    {
        ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START,
        ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START,
        ChunkStatus.STRUCTURE_START, ChunkStatus.STRUCTURE_START, ChunkStatus.EMPTY,
    };

    //GenerationStatus 该等级允许生成的最高状态 对应原版 generationStatus
    //超出半径返回 null 表示这个区块不参与生成
    public static ChunkStatus? GenerationStatus(int level)
        => GetStatusAroundFullChunk(level - FullChunkLevel, null);

    //GetStatusAroundFullChunk 距 FULL 给定距离上要求的最低状态 对应原版 getStatusAroundFullChunk
    public static ChunkStatus? GetStatusAroundFullChunk(int distanceToFullChunk, ChunkStatus? defaultValue)
    {
        if (distanceToFullChunk > RadiusAroundFullChunk) return defaultValue;
        if (distanceToFullChunk <= 0) return ChunkStatus.FULL;
        return AccumulatedDependencies[distanceToFullChunk];
    }

    //ByStatus 该状态对应的等级 对应原版 byStatus
    //只有累积依赖表里出现过的状态能换算 原版对其它状态是越界异常
    public static int ByStatus(ChunkStatus status)
        => status == ChunkStatus.FULL ? FullChunkLevel : FullChunkLevel + RadiusOf(status);

    //RadiusOf 该状态在累积依赖表里要求的半径 对应原版 ChunkDependencies.getRadiusOf
    private static int RadiusOf(ChunkStatus status)
    {
        if (status == ChunkStatus.EMPTY) return RadiusAroundFullChunk;
        if (status == ChunkStatus.STRUCTURE_START) return RadiusAroundFullChunk - 1;
        throw new ArgumentException($"状态 {status} 不在累积依赖范围内", nameof(status));
    }

    //FullStatus 该等级对应的加载档位 对应原版 fullStatus
    public static FullChunkStatus FullStatus(int level)
    {
        if (level <= EntityTickingLevel) return FullChunkStatus.EntityTicking;
        if (level <= BlockTickingLevel) return FullChunkStatus.BlockTicking;
        if (level <= FullChunkLevel) return FullChunkStatus.Full;
        return FullChunkStatus.Inaccessible;
    }

    //ByStatus 加载档位对应的等级 对应原版 byStatus(FullChunkStatus)
    public static int ByStatus(FullChunkStatus status)
    {
        if (status == FullChunkStatus.Inaccessible) return MaxLevel;
        if (status == FullChunkStatus.Full) return FullChunkLevel;
        if (status == FullChunkStatus.BlockTicking) return BlockTickingLevel;
        return EntityTickingLevel;
    }

    //IsEntityTicking 该等级能否 tick 实体 对应原版 isEntityTicking
    public static bool IsEntityTicking(int level) => level <= EntityTickingLevel;

    //IsBlockTicking 该等级能否 tick 方块 对应原版 isBlockTicking
    public static bool IsBlockTicking(int level) => level <= BlockTickingLevel;

    //IsLoaded 该等级是否在加载范围内 对应原版 isLoaded
    public static bool IsLoaded(int level) => level <= MaxLevel;
}
