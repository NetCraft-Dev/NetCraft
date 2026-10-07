using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureSet structure set, maps to vanilla net.minecraft.world.level.levelgen.structure.StructureSet
//A set = one placement config + a weighted pool of structures; only one of them is picked within the same generation step
//Maps to vanilla: each set has a single placement, not one per structure
public sealed record StructureSet(StructurePlacement Placement, IReadOnlyList<StructureSelectionEntry> Structures)
    : NetCraft.Registry.StructureSet
{
    //WeightTotal total weight, used as the random bound for weighted selection
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

//StructureSelectionEntry one entry of a set, maps to vanilla StructureSet.StructureSelectionEntry
//structure is a structure registry reference, weight is a positive integer weight
public sealed record StructureSelectionEntry(Holder<NetCraft.Registry.Structure> Structure, int Weight);
