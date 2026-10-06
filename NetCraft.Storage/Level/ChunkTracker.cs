using NetCraft.Primitives;

namespace NetCraft.Storage;

//ChunkTracker, chunk level propagation graph, maps to vanilla net.minecraft.server.level.ChunkTracker
//Ticket levels act as sources and decay by +1 per of the eight neighbors, so each chunk's level equals "how far from the nearest ticket source"
public abstract class ChunkTracker : DynamicGraphMinFixedPoint
{
    //InvalidChunkPos, invalid chunk pos sentinel, maps to vanilla ChunkPos.INVALID_CHUNK_POS
    public const long InvalidChunkPos = long.MaxValue;

    protected ChunkTracker(int levelCount, int minQueueSize, int minMapSize)
        : base(levelCount, minQueueSize, minMapSize) { }

    //GetLevelFromSource, the ticket level of that chunk, maps to vanilla getLevelFromSource
    protected abstract int GetLevelFromSource(long packedPos);

    protected override bool IsSource(long node) => node == InvalidChunkPos;

    protected override void CheckNeighborsAfterUpdate(long node, int level, bool onlyDecrease)
    {
        //Skip only when decreasing and already at the outermost ring; neighbors cannot be lower than this
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

    //Update re-enqueues the node after a ticket change, maps to vanilla update
    public void Update(long node, int newLevelFrom, bool onlyDecreased)
        => CheckEdge(InvalidChunkPos, node, newLevelFrom, onlyDecreased);
}
