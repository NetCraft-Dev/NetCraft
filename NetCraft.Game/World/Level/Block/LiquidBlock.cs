using NetCraft.Game.World.Level.Material;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Block;

//LiquidBlock fluid block, maps to vanilla net.minecraft.world.level.block.LiquidBlock
//The block state level 0-15 maps one-to-one with fluid states: 0 is source, 1-7 decrease stepwise, 8 is falling, 9-15 are never produced
//Fluid tick scheduling enters here; placement and neighbor changes must schedule the tick, otherwise poured water will not move
public abstract class LiquidBlock : BlockBehaviour
{
    //LevelProperty block state fluid level 0-15, maps to vanilla LiquidBlock.LEVEL
    public static readonly IntegerProperty LevelProperty = new("level", 0, 15);

    //SpreadDirections direction order for lava checking for adjacent water, follows vanilla POSSIBLE_FLOW_DIRECTIONS
    private static readonly Direction[] SpreadDirections =
    {
        Direction.Down, Direction.South, Direction.North, Direction.East, Direction.West,
    };

    private readonly FlowingFluid _fluid;

    //_stateCache maps block level to fluid state: 0 source, 1-7 decreasing, 8 falling
    //Built in the constructor; vanilla likewise prebuilds this table in the constructor, so getting a state is a pure lookup
    private readonly FluidState[] _stateCache = new FluidState[9];

    protected LiquidBlock(FlowingFluid fluid)
    {
        _fluid = fluid;
        _stateCache[0] = fluid.GetSourceState(false);
        for (var level = 1; level < 8; level++)
            _stateCache[level] = fluid.GetFlowingState(8 - level, false);
        _stateCache[8] = fluid.GetFlowingState(8, true);
    }

    //Fluid the fluid this block carries
    public FlowingFluid Fluid => _fluid;

    public override IDictionary<string, PropertyBase> Properties
        => new Dictionary<string, PropertyBase> { ["level"] = LevelProperty };

    public override bool HasFluidState => true;

    //In vanilla a fluid block is effectively unmineable; 100 is the vanilla destroyTime value
    public override float DestroySpeed => 100f;

    //GetFluidState looks up the fluid state from the block level, maps to vanilla getFluidState
    //level is a 0-15 block property but fluids only recognize 0-8, anything beyond is treated as 8
    public override FluidState GetFluidState(BlockState state)
        => _stateCache[Math.Min(state.GetValue(LevelProperty), 8)];

    //OnPlace schedules the fluid tick after placement, maps to vanilla onPlace
    public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState, bool movedByPiston)
        => ScheduleFluidTick(level, pos, state);

    //NeighborChanged reschedules the fluid tick after a neighbor changes, maps to vanilla neighborChanged
    public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
        NetCraft.Registry.Block changedBlock, bool movedByPiston)
        => ScheduleFluidTick(level, pos, state);

    //UpdateShape reschedules the fluid tick when itself or a neighbor is a source, maps to vanilla updateShape
    //A cell next to a source must be watched every tick; missing a single schedule stops the flow
    public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
        Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
    {
        var fluidState = GetFluidState(state);
        if (fluidState.IsSource || neighbourState.FluidState.IsSource)
            level.ScheduleTick(pos, fluidState.Type, _fluid.GetTickDelay(level));
        return state;
    }

    //Tick the block's own scheduled tick; vanilla maintains bubble columns here, bubble columns are not implemented, left empty
    public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random) { }

    //ScheduleFluidTick schedules the fluid tick when it should spread, maps to the vanilla shouldSpreadLiquid check plus scheduleTick
    private void ScheduleFluidTick(ServerLevel level, BlockPos pos, BlockState state)
    {
        if (!ShouldSpreadLiquid(level, pos, state)) return;
        level.ScheduleTick(pos, GetFluidState(state).Type, _fluid.GetTickDelay(level));
    }

    //ShouldSpreadLiquid lava solidifies into stone when it meets water; returning false means it spreads no further this time, maps to vanilla shouldSpreadLiquid
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
            //If the block table has no obsidian or cobblestone, only smoke is emitted and no conversion happens; unregistered blocks must not be written into the world
            if (converted is not null && !converted.IsAir)
                level.SetBlock(pos, converted.DefaultBlockState, 3);
            level.LevelEvent(1501, pos, 0);
            return false;
        }
        return true;
    }
}
