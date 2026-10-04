namespace NetCraft.Registry.State;

//FluidState 流体状态对应原版 net.minecraft.world.level.material.FluidState
//这里只保留雕刻与地表规则需要的两点 是不是流体 以及退回成方块时写哪个状态
//原版还带 level/ownHeight 等量 等流体刻与流动模拟接入时再补
public sealed class FluidState
{
    //Empty 空流体对应原版 Fluids.EMPTY.defaultFluidState
    public static readonly FluidState Empty = new();

    private readonly BlockState? _legacyBlock;

    private FluidState() { }

    //legacyBlock 是这团流体退回成方块时的状态 水的源是 level=0 的水方块
    public FluidState(BlockState legacyBlock) => _legacyBlock = legacyBlock;

    //IsEmpty 这格不是流体
    public bool IsEmpty => _legacyBlock is null;

    //IsWater 这团流体是不是水 对应原版 fluidState.is(FluidTags.WATER)
    //水的注册名只有 water 与 flowing_water 两种 流体标签体系还没接 这里按退回方块的注册名判定
    public bool IsWater => _legacyBlock is { } block
        && block.Owner.Id.Path is "water" or "flowing_water";

    //IsFull 这团流体是否占满一格 对应原版 fluidState.isFull
    //本作只有一种水状态没有流动水的液面高度 有水即视为满
    public bool IsFull => !IsEmpty;

    //CreateLegacyBlock 退回成方块状态对应原版 createLegacyBlock
    public BlockState CreateLegacyBlock()
        => _legacyBlock ?? throw new InvalidOperationException("空流体没有对应的方块状态");
}
