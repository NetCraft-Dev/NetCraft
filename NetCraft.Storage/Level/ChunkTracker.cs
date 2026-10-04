using NetCraft.Primitives;

namespace NetCraft.Storage;

//ChunkTracker 区块等级传播图对应原版 net.minecraft.server.level.ChunkTracker
//票等级当源 向八邻居每格加一衰减 于是每个区块的等级等价于"离最近的票源有多远"
public abstract class ChunkTracker : DynamicGraphMinFixedPoint
{
    //InvalidChunkPos 无效区块坐标哨兵 对应原版 ChunkPos.INVALID_CHUNK_POS
    public const long InvalidChunkPos = long.MaxValue;

    protected ChunkTracker(int levelCount, int minQueueSize, int minMapSize)
        : base(levelCount, minQueueSize, minMapSize) { }

    //GetLevelFromSource 该区块的票等级 对应原版 getLevelFromSource
    protected abstract int GetLevelFromSource(long packedPos);

    protected override bool IsSource(long node) => node == InvalidChunkPos;

    protected override void CheckNeighborsAfterUpdate(long node, int level, bool onlyDecrease)
    {
        //只在递减且已经到最外圈时跳过 邻居不可能比这更低
        if (onlyDecrease && level >= LevelCount - 2) return;
        var pos = ChunkPos.Unpack(node);
        for (var offsetX = -1; offsetX <= 1; offsetX++)
            for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                var neighbor = ChunkPos.Pack(pos.X + offsetX, pos.Z + offsetZ);
                if (neighbor != node) CheckNeighbor(node, neighbor, level, onlyDecrease);
            }
    }

    protected override int GetComputedLevel(long node, long knownParent, int knownLevelFromParent)
    {
        var computedLevel = knownLevelFromParent;
        var pos = ChunkPos.Unpack(node);
        for (var offsetX = -1; offsetX <= 1; offsetX++)
            for (var offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                var neighbor = ChunkPos.Pack(pos.X + offsetX, pos.Z + offsetZ);
                if (neighbor == node) neighbor = InvalidChunkPos;
                if (neighbor == knownParent) continue;
                var costFromNeighbor = ComputeLevelFromNeighbor(neighbor, node, GetLevel(neighbor));
                if (computedLevel > costFromNeighbor) computedLevel = costFromNeighbor;
                if (computedLevel == 0) return computedLevel;
            }
        return computedLevel;
    }

    protected override int ComputeLevelFromNeighbor(long from, long to, int fromLevel)
        => from == InvalidChunkPos ? GetLevelFromSource(to) : fromLevel + 1;

    //Update 票变化后把该节点重新入队 对应原版 update
    public void Update(long node, int newLevelFrom, bool onlyDecreased)
        => CheckEdge(InvalidChunkPos, node, newLevelFrom, onlyDecreased);
}
