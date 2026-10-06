namespace NetCraft.Registry.State;

//FluidState 流体状态 对应原版 net.minecraft.world.level.material.FluidState
//原版继承 StateHolder 带 LEVEL 与 FALLING 两个属性 这里直接持液面高度与下落标记两个字段
//流体状态只有这两个维度 不需要邻居表 结构简化不改变行为
public sealed class FluidState
{
    //Empty 空流体状态 对应原版 Fluids.EMPTY.defaultFluidState
    //走 Fluid.Empty 的状态表 保证与注册表里 empty 那一项是同一个实例
    public static FluidState Empty => Fluid.Empty.DefaultFluidState;

    public FluidState(Fluid type, int amount, bool falling)
    {
        Type = type;
        Amount = amount;
        Falling = falling;
    }

    //Type 该状态属于哪种流体
    public Fluid Type { get; }

    //Amount 液面高度 0-8 源为 8 对应原版 LEVEL
    public int Amount { get; }

    //Falling 是否处于下落状态 从上方直接灌下来的水落面更高 对应原版 FALLING
    public bool Falling { get; }

    //IsEmpty 这格没有流体
    public bool IsEmpty => Type.IsEmpty;

    //IsSource 这格是无限源 对应原版 isSource
    public bool IsSource => Type.IsSource(this);

    //IsFull 液面已满 对应原版 isFull
    public bool IsFull => Amount >= 8;

    //IsWater 这团流体是不是水 对应原版 fluidState.is(FluidTags.WATER)
    //流体标签体系还没接 按注册名判定 水的注册名只有 water 与 flowing_water 两种
    public bool IsWater => Type.Id.Path is "water" or "flowing_water";

    //OwnHeight 自身液面高度比例 不考虑上方同族流体叠加 对应原版 getOwnHeight
    public float OwnHeight => Type.GetOwnHeight(this);

    //CreateLegacyBlock 退回成方块状态对应原版 createLegacyBlock
    public BlockState CreateLegacyBlock() => Type.CreateLegacyBlock(this);

    //SetAmount 复制出不同液面高度的状态 液的扩散就是逐级降档
    public FluidState SetAmount(int amount) => new(Type, amount, Falling);

    //SetFalling 复制出带下落标记的状态
    public FluidState SetFalling(bool falling) => new(Type, Amount, falling);

    public override string ToString() => IsEmpty ? "empty" : $"{Type.Id}[level={Amount},falling={Falling}]";
}
