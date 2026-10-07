using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.Server;

//ServerBlockTicks random tick scheduling, maps to the block random tick part of vanilla ServerLevel.tickChunk
//Samples random positions per section, calling back only blocks with RandomTicks true; the sample count equals randomTickSpeed
//Vanilla ticks only entity-ticking chunks by ticket level; this narrows by the caller-provided candidate chunks, see tickingChunks
public static class ServerBlockTicks
{
    //DefaultRandomTickSpeed maps to the vanilla randomTickSpeed gamerule default
    public const int DefaultRandomTickSpeed = 3;

    //RandomTick advances one frame of random ticks and returns the number of blocks actually triggered
    //tickingChunks null means no narrowing (no-player scenarios and tests use the full set); non-null samples only chunks in the set
    //Without narrowing it traverses every section of every loaded chunk each tick sampling speed times; the sample volume grows linearly with load
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
