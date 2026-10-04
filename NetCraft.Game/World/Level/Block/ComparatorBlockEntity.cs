using NetCraft.Nbt;
using NetCraft.Primitives;

namespace NetCraft.Game.World.Level.Block;

//ComparatorBlockEntity 比较器方块实体 对应原版 ComparatorBlockEntity
//只存一个输出值 比较器的模拟输出要跨刻保留 方块状态里放不下 0-15 的中间结果
public sealed class ComparatorBlockEntity : BlockEntity
{
    public ComparatorBlockEntity(BlockPos pos) : base(BlockEntityTypes.COMPARATOR, pos) { }

    //OutputSignal 上次算出的输出强度 未算过是 0 对应原版 output
    public int OutputSignal { get; private set; }

    //SetOutputSignal 写入输出强度 对应原版 setOutputSignal
    public void SetOutputSignal(int value) => OutputSignal = value;

    //SaveAdditional 存盘字段名与原版一致 对应原版 saveAdditional
    public override void SaveAdditional(CompoundTag tag)
    {
        base.SaveAdditional(tag);
        tag.PutInt("OutputSignal", OutputSignal);
    }

    //LoadAdditional 读回输出强度 缺字段按 0 对应原版 loadAdditional
    public override void LoadAdditional(CompoundTag tag)
    {
        base.LoadAdditional(tag);
        OutputSignal = tag.GetIntOr("OutputSignal", 0);
    }
}
