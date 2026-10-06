using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.Block;

//ComparatorBlockEntity comparator block entity, maps to vanilla ComparatorBlockEntity
//Stores only an output value; the comparator's analog output must persist across ticks and the 0-15 intermediate result does not fit in a block state
public sealed class ComparatorBlockEntity : BlockEntity
{
    public ComparatorBlockEntity(BlockPos pos) : base(BlockEntityTypes.COMPARATOR, pos) { }

    //OutputSignal last computed output strength, 0 if never computed, maps to vanilla output
    public int OutputSignal { get; private set; }

    //SetOutputSignal writes the output strength, maps to vanilla setOutputSignal
    public void SetOutputSignal(int value) => OutputSignal = value;

    //SaveAdditional disk field names match vanilla, maps to vanilla saveAdditional
    public override void SaveAdditional(CompoundTag tag)
    {
        base.SaveAdditional(tag);
        tag.PutInt("OutputSignal", OutputSignal);
    }

    //LoadAdditional reads back the output strength, defaults to 0 when missing, maps to vanilla loadAdditional
    public override void LoadAdditional(CompoundTag tag)
    {
        base.LoadAdditional(tag);
        OutputSignal = tag.GetIntOr("OutputSignal", 0);
    }
}
