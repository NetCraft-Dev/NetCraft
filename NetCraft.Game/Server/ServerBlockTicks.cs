using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.Server;

//ServerBlockTicks 随机刻调度对应原版 ServerLevel.tickChunk 里的方块随机刻部分
//按区段抽样随机位置 只对 RandomTicks 为 true 的方块回调 抽样次数等于 randomTickSpeed
//原版按 ticket 分级只 tick 实体 ticking 区块 本作按调用方给的候选区块收窄 见 tickingChunks
public static class ServerBlockTicks
{
    //DefaultRandomTickSpeed 对应原版 randomTickSpeed 游戏规则默认值
    public const int DefaultRandomTickSpeed = 3;

    //RandomTick 推进一帧随机刻 返回实际触发的方块数
    //tickingChunks 为 null 表示不收窄（无玩家场景与测试走全量）非 null 时只抽样集合内的区块
    //不收窄等于每 tick 遍历全部已加载区块的每个区段各抽 speed 次 抽样量随加载量线性涨
    public static int RandomTick(PersistentServerLevel level, RandomSource random,
        int speed = DefaultRandomTickSpeed, IReadOnlySet<long>? tickingChunks = null)
    {
        if (speed <= 0) return 0;
        var triggered = 0;
        foreach (var chunk in level.ChunkSource.LoadedChunks)
        {
            if (tickingChunks is not null && !tickingChunks.Contains(chunk.Pos.Pack())) continue;
            for (var sectionY = chunk.MinSectionY; sectionY <= chunk.MaxSectionY; sectionY++)
            {
                var section = chunk.GetSection(sectionY);
                if (section is null || section.HasOnlyAir()) continue;
                for (var i = 0; i < speed; i++)
                {
                    var x = random.NextInt(16);
                    var y = random.NextInt(16);
                    var z = random.NextInt(16);
                    var state = section.GetBlockState(x, y, z);
                    if (state.Owner is not BlockBehaviour behaviour || !behaviour.RandomTicks) continue;
                    var pos = new BlockPos(chunk.Pos.X * 16 + x, sectionY * 16 + y, chunk.Pos.Z * 16 + z);
                    behaviour.RandomTick(level, pos, state, random);
                    triggered++;
                }
            }
        }
        return triggered;
    }
}
