using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructurePlacementRegistry 结构集合注册表 对应原版 ChunkGenerator 持有的 List<StructureSet>
//世界用到的全部结构集合都在这里 区块装配时按它筛出命中的集合
public sealed class StructurePlacementRegistry
{
    private readonly List<NetCraft.Registry.StructureSet> _sets = new();

    public long Seed { get; }

    //State 放置判定状态 持世界种子供 placement 做排斥区查询
    public ChunkGeneratorStructureState State { get; }

    public IReadOnlyList<NetCraft.Registry.StructureSet> Sets => _sets;

    public StructurePlacementRegistry(long seed)
    {
        Seed = seed;
        State = new ChunkGeneratorStructureState(seed);
    }

    //AddSet 追加一个集合
    public void AddSet(NetCraft.Registry.StructureSet set) => _sets.Add(set);

    //GetSetsForChunk 取命中该区块的集合 对应原版 createStructures 的集合筛选
    //排斥区判定会回头查本注册表里的其他集合 因此必须在同一个 state 下判定
    public List<StructureSet> GetSetsForChunk(ChunkPos pos)
    {
        var result = new List<StructureSet>();
        foreach (var set in _sets)
        {
            if (set is StructureSet gameSet && gameSet.Placement.IsStructureChunk(State, pos.X, pos.Z))
                result.Add(gameSet);
        }
        return result;
    }

    //Empty 空注册表 不生成任何结构
    public static StructurePlacementRegistry Empty { get; } = new(0L);
}
