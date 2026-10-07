using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Material;

//FlowingFluid base for fluids that flow, maps to vanilla net.minecraft.world.level.material.FlowingFluid
//Spread, surface drop-off, backward slope search, source formation and blockage checks all live here; water and lava only supply their own levels and delays
//Vanilla takes a BlockGetter so the same algorithm works during generation; this port always uses ServerLevel
//Blockage uses the block's own occlusion shape rather than a position-dependent collision shape; they match for solid blocks but may differ for slabs and fences
public abstract class FlowingFluid : Fluid, IFluidBehaviour
{
    //Four horizontal directions, ordered like vanilla Direction.Plane.HORIZONTAL
    protected static readonly Direction[] HorizontalDirections =
    {
        Direction.North, Direction.East, Direction.South, Direction.West,
    };

    //FlowingType flowing variant, maps to vanilla getFlowing
    public abstract Fluid FlowingType { get; }

    //SourceType source variant, maps to vanilla getSource
    public abstract Fluid SourceType { get; }

    //GetDropOff levels lost per block outward, 1 for water and 2 for lava, maps to vanilla getDropOff
    public abstract int GetDropOff(ServerLevel level);

    //GetSlopeFindDistance max layers of backward slope search, 4 for water and 2 for lava, maps to vanilla getSlopeFindDistance
    public abstract int GetSlopeFindDistance(ServerLevel level);

    //GetTickDelay ticks between two flow steps, 5 for water and 30 for lava, maps to vanilla getTickDelay
    public abstract int GetTickDelay(ServerLevel level);

    //GetHeight actual surface height of the fluid in this cell, maps to vanilla getHeight
    public abstract float GetHeight(FluidState state, ServerLevel level, BlockPos pos);

    //GetFlow flow direction of the fluid in this cell, maps to vanilla getFlow
    public abstract Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state);

    //CanBeReplacedWith whether the fluid here can be displaced by another fluid, maps to vanilla canBeReplacedWith
    public abstract bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction);

    //CanConvertToSource whether a new source forms in place when two or more adjacent sources exist, maps to vanilla canConvertToSource
    protected abstract bool CanConvertToSource(ServerLevel level);

    //BeforeDestroyingBlock work done to a block before the fluid submerges it, maps to vanilla beforeDestroyingBlock
    protected virtual void BeforeDestroyingBlock(ServerLevel level, BlockPos pos, BlockState state) { }

    public virtual bool IsRandomlyTicking => false;

    public virtual void RandomTick(ServerLevel level, BlockPos pos, FluidState fluidState, RandomSource random) { }

    //GetFlowingState gets the flowing state, maps to vanilla getFlowing(amount, falling)
    public FluidState GetFlowingState(int amount, bool falling) => FlowingType.GetStateOf(amount, falling);

    //GetSourceState gets the source state, maps to vanilla getSource(falling)
    public FluidState GetSourceState(bool falling) => SourceType.GetStateOf(8, falling);

    //GetSpreadDelay ticks until this spread runs, maps to vanilla getSpreadDelay
    public virtual int GetSpreadDelay(ServerLevel level, BlockPos pos, FluidState oldState, FluidState newState)
        => GetTickDelay(level);

    //GetLegacyLevel derives the block-state level 0-15 from the fluid state, maps to vanilla getLegacyLevel
    //0 is the source, 1-7 are progressively lower flowing levels, 8 is falling water
    protected static int GetLegacyLevel(FluidState state)
        => state.IsSource ? 0 : 8 - Math.Min(state.Amount, 8) + (state.Falling ? 8 : 0);

    //Tick scheduled tick of the fluid, maps to vanilla FlowingFluid.tick
    public void Tick(ServerLevel level, BlockPos pos, BlockState blockState, FluidState fluidState)
    {
        if (!fluidState.IsSource)
        {
            var newState = GetNewLiquid(level, pos, StateAt(level, pos));
            var delay = GetSpreadDelay(level, pos, fluidState, newState);
            if (newState.IsEmpty)
            {
                fluidState = newState;
                blockState = Blocks.AIR.DefaultBlockState;
                level.SetBlock(pos, blockState, 3);
            }
            else if (!ReferenceEquals(newState, fluidState))
            {
                fluidState = newState;
                blockState = fluidState.CreateLegacyBlock();
                level.SetBlock(pos, blockState, 3);
                level.ScheduleTick(pos, fluidState.Type, delay);
            }
        }
        Spread(level, pos, blockState, fluidState);
    }

    //Spread one spread step, try down first then the sides, maps to vanilla spread
    protected void Spread(ServerLevel level, BlockPos pos, BlockState state, FluidState fluidState)
    {
        if (fluidState.IsEmpty) return;
        var belowPos = pos.Offset(Direction.Down);
        var belowState = StateAt(level, belowPos);
        var belowFluid = level.GetFluidState(belowPos);
        var newBelow = GetNewLiquid(level, belowPos, belowState);
        if (CanMaybePassThrough(state, Direction.Down, belowState, belowFluid)
            && CanBeReplacedAt(belowFluid, level, belowPos, newBelow.Type, Direction.Down)
            && CanHoldSpecificFluid(belowState, newBelow.Type))
        {
            SpreadTo(level, belowPos, belowState, Direction.Down, newBelow);
            //A drain whose three sides above are all sources need not spread sideways; the water pours down the whole column, maps to vanilla sourceNeighborCount >= 3
            if (SourceNeighborCount(level, pos) >= 3) SpreadToSides(level, pos, fluidState, state);
            return;
        }
        if (fluidState.IsSource || !IsWaterHole(level, pos, state, belowPos, belowState))
            SpreadToSides(level, pos, fluidState, state);
    }

    //SpreadToSides spread to the sides, drop levels by the fall first, falling fluid is pinned at level 7, maps to vanilla spreadToSides
    private void SpreadToSides(ServerLevel level, BlockPos pos, FluidState fluidState, BlockState state)
    {
        var amount = fluidState.Amount - GetDropOff(level);
        if (fluidState.Falling) amount = 7;
        if (amount <= 0) return;
        foreach (var (direction, newFluid) in GetSpread(level, pos, state))
        {
            var neighbourPos = pos.Offset(direction);
            SpreadTo(level, neighbourPos, StateAt(level, neighbourPos), direction, newFluid);
        }
    }

    //GetNewLiquid computes the final fluid state for this cell, maps to vanilla getNewLiquid
    //Takes the highest level among same-family neighbours minus one drop-off, a full dropping column above forces level 8, and two or more adjacent sources raise this cell to a source when conversion is allowed
    public FluidState GetNewLiquid(ServerLevel level, BlockPos pos, BlockState state)
    {
        var highest = 0;
        var sources = 0;
        foreach (var direction in HorizontalDirections)
        {
            var relativePos = pos.Offset(direction);
            var relativeState = StateAt(level, relativePos);
            var relativeFluid = level.GetFluidState(relativePos);
            if (!relativeFluid.Type.IsSame(this)) continue;
            if (!CanPassThroughWall(direction, state, relativeState)) continue;
            if (relativeFluid.IsSource) sources++;
            highest = Math.Max(highest, relativeFluid.Amount);
        }

        if (sources >= 2 && CanConvertToSource(level))
        {
            var belowPos = pos.Offset(Direction.Down);
            var belowState = StateAt(level, belowPos);
            var belowFluid = level.GetFluidState(belowPos);
            if (IsSolid(belowState) || (belowFluid.Type.IsSame(this) && belowFluid.IsSource))
                return GetSourceState(false);
        }

        var abovePos = pos.Offset(Direction.Up);
        var aboveState = StateAt(level, abovePos);
        var aboveFluid = level.GetFluidState(abovePos);
        if (!aboveFluid.IsEmpty && aboveFluid.Type.IsSame(this)
            && CanPassThroughWall(Direction.Up, state, aboveState))
            return GetFlowingState(8, true);

        var amount = highest - GetDropOff(level);
        return amount <= 0 ? FluidState.Empty : GetFlowingState(amount, false);
    }

    //CanPassThroughWall whether a fluid can pass between two faces; two full blocks block each other, maps to vanilla canPassThroughWall
    private static bool CanPassThroughWall(Direction direction, BlockState sourceState, BlockState targetState)
    {
        var targetShape = OcclusionOf(targetState);
        if (ReferenceEquals(targetShape, Shapes.Block())) return false;
        var sourceShape = OcclusionOf(sourceState);
        if (ReferenceEquals(sourceShape, Shapes.Block())) return false;
        if (ReferenceEquals(sourceShape, Shapes.Empty()) && ReferenceEquals(targetShape, Shapes.Empty())) return true;
        return !Shapes.MergedFaceOccludes(sourceShape, targetShape, direction);
    }

    //OcclusionOf occlusion shape of this state; blocks that do not occlude light count as empty
    //Same semantics as Block.SolidRender; water and lava report CanOcclude false so they do not block themselves
    private static VoxelShape OcclusionOf(BlockState state)
        => state.Owner.CanOcclude ? state.Owner.GetOcclusionShape(state) : Shapes.Empty();

    //SpreadTo writes the fluid into the target cell, maps to vanilla spreadTo
    protected virtual void SpreadTo(ServerLevel level, BlockPos pos, BlockState state, Direction direction, FluidState target)
    {
        if (!state.Owner.IsAir) BeforeDestroyingBlock(level, pos, state);
        level.SetBlock(pos, target.CreateLegacyBlock(), 3);
    }

    //GetSlopeDistance layers walked along the terrain to find a landing spot; returns the current layer once a hole is found, maps to vanilla getSlopeDistance
    private int GetSlopeDistance(ServerLevel level, BlockPos pos, int pass, Direction from, BlockState state)
    {
        var lowest = 1000;
        foreach (var direction in HorizontalDirections)
        {
            if (direction == from) continue;
            var testPos = pos.Offset(direction);
            var testState = StateAt(level, testPos);
            var testFluid = level.GetFluidState(testPos);
            if (!CanPassThrough(FlowingType, state, direction, testState, testFluid)) continue;
            if (IsHole(level, testPos)) return pass;
            if (pass >= GetSlopeFindDistance(level)) continue;
            var found = GetSlopeDistance(level, testPos, pass + 1, direction.Opposite, testState);
            if (found < lowest) lowest = found;
        }
        return lowest;
    }

    //IsWaterHole whether this cell can leak straight down, maps to vanilla isWaterHole
    private bool IsWaterHole(ServerLevel level, BlockPos topPos, BlockState topState, BlockPos bottomPos, BlockState bottomState)
    {
        if (!CanPassThroughWall(Direction.Down, topState, bottomState)) return false;
        if (bottomState.FluidState.Type.IsSame(this)) return true;
        return CanHoldFluid(bottomState, FlowingType);
    }

    //IsHole whether the cell directly below can leak, maps to vanilla SpreadContext.isHole
    private bool IsHole(ServerLevel level, BlockPos pos)
    {
        var state = StateAt(level, pos);
        return IsWaterHole(level, pos, state, pos.Offset(Direction.Down), StateAt(level, pos.Offset(Direction.Down)));
    }

    //CanPassThrough whether this fluid can pass through the cell, maps to vanilla canPassThrough
    private bool CanPassThrough(Fluid fluid, BlockState sourceState, Direction direction, BlockState testState, FluidState testFluidState)
        => CanMaybePassThrough(sourceState, direction, testState, testFluidState)
            && CanHoldSpecificFluid(testState, fluid);

    //CanMaybePassThrough excludes same-family source cells and shapes that cannot hold fluid, maps to vanilla canMaybePassThrough
    private bool CanMaybePassThrough(BlockState sourceState, Direction direction, BlockState testState, FluidState testFluidState)
        => !IsSourceBlockOfThisType(testFluidState)
            && CanHoldAnyFluid(testState)
            && CanPassThroughWall(direction, sourceState, testState);

    //IsSourceBlockOfThisType whether the state is a source of this fluid, maps to vanilla isSourceBlockOfThisType
    private bool IsSourceBlockOfThisType(FluidState state)
        => state.Type.IsSame(this) && state.IsSource;

    //SourceNeighborCount number of same-family sources around, maps to vanilla sourceNeighborCount
    private int SourceNeighborCount(ServerLevel level, BlockPos pos)
    {
        var count = 0;
        foreach (var direction in HorizontalDirections)
            if (IsSourceBlockOfThisType(level.GetFluidState(pos.Offset(direction)))) count++;
        return count;
    }

    //GetSpread computes the state each neighbour should receive; closer drops take priority, maps to vanilla getSpread
    protected Dictionary<Direction, FluidState> GetSpread(ServerLevel level, BlockPos pos, BlockState state)
    {
        var lowest = 1000;
        var result = new Dictionary<Direction, FluidState>();
        foreach (var direction in HorizontalDirections)
        {
            var testPos = pos.Offset(direction);
            var testState = StateAt(level, testPos);
            var testFluid = level.GetFluidState(testPos);
            if (!CanMaybePassThrough(state, direction, testState, testFluid)) continue;
            var newFluid = GetNewLiquid(level, testPos, testState);
            if (!CanHoldSpecificFluid(testState, newFluid.Type)) continue;
            var distance = IsHole(level, testPos) ? 0 : GetSlopeDistance(level, testPos, 1, direction.Opposite, testState);
            if (distance < lowest) result.Clear();
            if (distance > lowest) continue;
            if (CanBeReplacedAt(testFluid, level, testPos, newFluid.Type, direction))
                result[direction] = newFluid;
            lowest = distance;
        }
        return result;
    }

    //CanHoldAnyFluid whether the block can hold any fluid; doors, signs, ladders, sugar cane and portals do not count, maps to vanilla canHoldAnyFluid
    private static bool CanHoldAnyFluid(BlockState state)
    {
        var block = state.Owner;
        if (block is BlockBehaviour behaviour && behaviour.HasCollision) return false;
        var path = block.Id.Path;
        return path is not ("ladder" or "sugar_cane" or "bubble_column" or "nether_portal"
            or "end_portal" or "end_gateway" or "structure_void")
            && !path.EndsWith("_sign") && !path.EndsWith("_door");
    }

    //CanHoldFluid whether the block can hold this particular fluid, maps to vanilla canHoldFluid
    private static bool CanHoldFluid(BlockState state, Fluid fluid)
        => CanHoldAnyFluid(state) && CanHoldSpecificFluid(state, fluid);

    //CanHoldSpecificFluid any extra restriction this block places on the fluid
    //Vanilla asks for LiquidBlockContainer (bubble columns, kelp); none are implemented yet so it is always true
    private static bool CanHoldSpecificFluid(BlockState state, Fluid fluid) => true;

    //CanBeReplacedAt whether the fluid currently in a cell can be displaced, maps to vanilla FluidState.canBeReplacedWith
    //An empty fluid is always displaceable via EmptyFluid's default in vanilla; here EmptyFluid cannot attach the behaviour interface at the registry layer so this short-circuits
    private static bool CanBeReplacedAt(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction)
        => state.IsEmpty
            || (state.Type is IFluidBehaviour behaviour && behaviour.CanBeReplacedWith(state, level, pos, other, direction));

    //IsSolid whether this block state counts as solid, used by the source formation check
    private static bool IsSolid(BlockState state)
        => state.Owner is BlockBehaviour behaviour && behaviour.HasCollision;

    //StateAt reads a block state, treating unloaded chunks as air; fluid ticks never run at unloaded positions
    private static BlockState StateAt(ServerLevel level, BlockPos pos)
        => level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;

    //HasSameAbove same-family fluid pressing from above, maps to vanilla hasSameAbove
    protected static bool HasSameAbove(FluidState fluidState, ServerLevel level, BlockPos pos)
        => fluidState.Type.IsSame(level.GetFluidState(pos.Offset(Direction.Up)).Type);
}
