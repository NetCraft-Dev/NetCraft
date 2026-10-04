using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//ChunkGeneratorStructureState 结构生成状态 对应原版 net.minecraft.world.level.chunk.ChunkGeneratorStructureState
//持有世界种子与结构集合列表 供放置判定与排斥区查询
//本作只保留判定需要的部分 环形放置的预设位置等接入要塞时再补
public sealed class ChunkGeneratorStructureState
{
    //LevelSeed 世界种子 全部放置判定都从它派生
    public long LevelSeed { get; }

    public ChunkGeneratorStructureState(long levelSeed) => LevelSeed = levelSeed;

    //HasStructureChunkInRange 范围内是否存在目标集合的放置点 对应原版 hasStructureChunkInRange
    //逐个区块问目标集合的 placement 命中 是排斥区判定的唯一依据
    public bool HasStructureChunkInRange(Holder<NetCraft.Registry.StructureSet> structureSet,
        int sourceX, int sourceZ, int range)
    {
        if (!structureSet.IsBound() || structureSet.Value is not StructureSet set) return false;
        var placement = set.Placement;
        for (var testX = sourceX - range; testX <= sourceX + range; testX++)
        {
            for (var testZ = sourceZ - range; testZ <= sourceZ + range; testZ++)
            {
                if (placement.IsStructureChunk(this, testX, testZ)) return true;
            }
        }
        return false;
    }
}
