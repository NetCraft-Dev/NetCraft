using NetCraft.Game.World.Level.Material;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Block;

//LiquidBlock 流体方块 对应原版 net.minecraft.world.level.block.LiquidBlock
//方块状态的 level 0-15 与流体状态一一对应 0 是源 1-7 逐级递减 8 是下落 9-15 不会产生
//流体刻的排入入口在这里 放置与邻接变化都要把刻排上 否则倒下去的水不会动
public class LiquidBlock : BlockBehaviour
{
    //LevelProperty 方块状态的液面档 0-15 对应原版 LiquidBlock.LEVEL
    public static readonly IntegerProperty LevelProperty = new("level", 0, 15);

    //SpreadDirections 岩浆检查四周是否遇水的方向序 照原版 POSSIBLE_FLOW_DIRECTIONS
    private static readonly Direction[] SpreadDirections =
    {
        Direction.Down, Direction.South, Direction.North, Direction.East, Direction.West,
    };

    private readonly FlowingFluid _fluid;

    //_stateCache 方块 level 到流体状态的映射 0 源 1-7 递减 8 下落
    //构造时就建好 原版同样在构造里预建这张表 取状态是纯查表
    private readonly FluidState[] _stateCache = new FluidState[9];

    protected LiquidBlock(FlowingFluid fluid)
    {
        _fluid = fluid;
        _stateCache[0] = fluid.GetSourceState(false);
        for (var level = 1; level < 8; level++)
            _stateCache[level] = fluid.GetFlowingState(8 - level, false);
        _stateCache[8] = fluid.GetFlowingState(8, true);
    }

    //Fluid 该方块承载的流体
    public FlowingFluid Fluid => _fluid;

    public override IDictionary<string, PropertyBase> Properties
        => new Dictionary<string, PropertyBase> { ["level"] = LevelProperty };

    public override bool HasFluidState => true;

    //原版流体方块挖起来等于挖不动 100 是原版 destroyTime 的取值
    public override float DestroySpeed => 100f;

    //GetFluidState 方块 level 反查流体状态 对应原版 getFluidState
    //level 是 0-15 的方块属性 流体只认 0-8 超出部分按 8 处理
    public override FluidState GetFluidState(BlockState state)
        => _stateCache[Math.Min(state.GetValue(LevelProperty), 8)];

    //OnPlace 放置到位后把流体刻排上 对应原版 onPlace
    public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState, bool movedByPiston)
        => ScheduleFluidTick(level, pos, state);

    //NeighborChanged 邻接方块变化后重排流体刻 对应原版 neighborChanged
    public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
        NetCraft.Registry.Block changedBlock, bool movedByPiston)
        => ScheduleFluidTick(level, pos, state);

    //UpdateShape 自身或邻接是源时重排流体刻 对应原版 updateShape
    //源格旁边必须每刻都有人盯着 少排一次水就断流
    public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
        Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
    {
        var fluidState = GetFluidState(state);
        if (fluidState.IsSource || neighbourState.FluidState.IsSource)
            level.ScheduleTick(pos, fluidState.Type, _fluid.GetTickDelay(level));
        return state;
    }

    //Tick 方块自身的调度刻 原版在这里维护气泡柱 气泡柱未实现 留空
    public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random) { }

    //ScheduleFluidTick 该扩散就把流体刻排上 对应原版 shouldSpreadLiquid 判定加 scheduleTick
    private void ScheduleFluidTick(ServerLevel level, BlockPos pos, BlockState state)
    {
        if (!ShouldSpreadLiquid(level, pos, state)) return;
        level.ScheduleTick(pos, GetFluidState(state).Type, _fluid.GetTickDelay(level));
    }

    //ShouldSpreadLiquid 岩浆四周遇水会凝固成石 返回 false 表示这次不再扩散 对应原版 shouldSpreadLiquid
    private bool ShouldSpreadLiquid(ServerLevel level, BlockPos pos, BlockState state)
    {
        if (_fluid.Id.Path is not ("lava" or "flowing_lava")) return true;
        foreach (var direction in SpreadDirections)
        {
            var neighbourPos = pos.Offset(direction.Opposite);
            if (!level.GetFluidState(neighbourPos).IsWater) continue;
            var fluidState = GetFluidState(state);
            var converted = BuiltInRegistries.BLOCK.GetValue(
                Identifier.WithDefaultNamespace(fluidState.IsSource ? "obsidian" : "cobblestone"));
            if (!converted.IsAir) level.SetBlock(pos, converted.DefaultBlockState, 3);
            level.LevelEvent(1501, pos, 0);
            return false;
        }
        return true;
    }
}
