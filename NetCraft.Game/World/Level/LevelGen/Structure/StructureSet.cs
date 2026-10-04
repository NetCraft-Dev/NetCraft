using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureSet 结构集合 对应原版 net.minecraft.world.level.levelgen.structure.StructureSet
//一个集合 = 一个放置配置 + 一组按权重抽取的结构 生成时同一步内只会挑中其中一个
//对应原版每个集合只有一份 placement 不是每个结构一份
public sealed record StructureSet(StructurePlacement Placement, IReadOnlyList<StructureSelectionEntry> Structures)
    : NetCraft.Registry.StructureSet
{
    //WeightTotal 权重总和 按权重抽取时用作随机上界
    public int WeightTotal
    {
        get
        {
            var total = 0;
            foreach (var entry in Structures) total += entry.Weight;
            return total;
        }
    }
}

//StructureSelectionEntry 集合里的一项 对应原版 StructureSet.StructureSelectionEntry
//structure 是结构注册表引用 weight 是正整数权重
public sealed record StructureSelectionEntry(Holder<NetCraft.Registry.Structure> Structure, int Weight);
