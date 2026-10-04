using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Server;

//SpawnFinder 新世界出生点搜索 对应原版 MinecraftServer.setInitialSpawn 的候选区块扫描
//从建议落点起按原版螺旋顺序逐区块找能站人的地表 命中即停
public static class SpawnFinder
{
    //CandidateRadius 候选区块半径 对应原版 Mth.square(11) 的 ±5
    private const int CandidateRadius = 5;

    //Find 找安全出生点 找不到返回 null 由调用方保留原值
    //suggestion 是建议落点 原版由噪声采样给出 本作取当前出生点
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
            //原版螺旋步进 到拐点换向 顺序参与选点结果不能改
            if (offsetX == offsetZ || (offsetX < 0 && offsetX == -offsetZ)
                || (offsetX > 0 && offsetX == 1 - offsetZ))
                (stepX, stepZ) = (-stepZ, stepX);
            offsetX += stepX;
            offsetZ += stepZ;
        }
        return null;
    }

    //FindInChunk 逐列扫区块找第一个安全落点 对应原版 PlayerSpawnFinder.getSpawnPosInChunk
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

    //FindColumn 单列找能站的地表 对应原版 PlayerSpawnFinder.getLevelRespawnPos
    //MOTION_BLOCKING 给出可站立面 WORLD_SURFACE 与 OCEAN_FLOOR 用来排除水面
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
            //直接读区块 出生点搜索跑在主循环之前 关卡缓存可能还没并入
            if (chunk.GetSection(y >> 4)?.GetBlockState(localX, y & 15, localZ) is not { } state) continue;
            //顶上压着流体说明这列是水面 直接放弃该列
            if (!state.FluidState.IsEmpty) break;
            var pos = new BlockPos(x, y, z);
            if (Block.IsFaceFull(CollisionContext.Empty.GetCollisionShape(state, collision, pos), Direction.Up))
                return new BlockPos(x, y + 1, z);
        }
        return null;
    }
}
