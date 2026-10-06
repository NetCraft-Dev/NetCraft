using NetCraft.Game.World.Level.Block;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.Material;

//FlowingFluid 会流动的流体基类 对应原版 net.minecraft.world.level.material.FlowingFluid
//扩散 液面降档 逆流搜索 水源生成与阻塞判定都在这里 水与岩浆只给各自的档位与延迟参数
//原版用 BlockGetter 参数让同一套算法复用到生成期 本作一律走 ServerLevel
//阻塞判定用方块自身遮挡形状 不查按位置变化的碰撞形状 实心方块两者一致 台阶栅栏一类会有偏差
public abstract class FlowingFluid : Fluid, IFluidBehaviour
{
    //四个水平方向 顺序照原版 Direction.Plane.HORIZONTAL
    protected static readonly Direction[] HorizontalDirections =
    {
        Direction.North, Direction.East, Direction.South, Direction.West,
    };

    //FlowingType 流动态实例 对应原版 getFlowing
    public abstract Fluid FlowingType { get; }

    //SourceType 源态实例 对应原版 getSource
    public abstract Fluid SourceType { get; }

    //GetDropOff 每向外流一格损失多少档 水 1 岩浆 2 对应原版 getDropOff
    public abstract int GetDropOff(ServerLevel level);

    //GetSlopeFindDistance 逆流搜索的最大层数 水 4 岩浆 2 对应原版 getSlopeFindDistance
    public abstract int GetSlopeFindDistance(ServerLevel level);

    //GetTickDelay 两次流动之间隔多少刻 水 5 岩浆 30 对应原版 getTickDelay
    public abstract int GetTickDelay(ServerLevel level);

    //GetHeight 该格流体的实际液面高度 对应原版 getHeight
    public abstract float GetHeight(FluidState state, ServerLevel level, BlockPos pos);

    //GetFlow 该格流体的流向 对应原版 getFlow
    public abstract Vec3 GetFlow(ServerLevel level, BlockPos pos, FluidState state);

    //CanBeReplacedWith 该格流体能不能被另一种流体顶掉 对应原版 canBeReplacedWith
    public abstract bool CanBeReplacedWith(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction);

    //CanConvertToSource 两格以上相邻源时能否原地生成新源 对应原版 canConvertToSource
    protected abstract bool CanConvertToSource(ServerLevel level);

    //BeforeDestroyingBlock 流体淹没一个方块前对它做的事 对应原版 beforeDestroyingBlock
    protected virtual void BeforeDestroyingBlock(ServerLevel level, BlockPos pos, BlockState state) { }

    public virtual bool IsRandomlyTicking => false;

    public virtual void RandomTick(ServerLevel level, BlockPos pos, FluidState fluidState, RandomSource random) { }

    //GetFlowingState 取流动态状态 对应原版 getFlowing(amount, falling)
    public FluidState GetFlowingState(int amount, bool falling) => FlowingType.GetStateOf(amount, falling);

    //GetSourceState 取源态状态 对应原版 getSource(falling)
    public FluidState GetSourceState(bool falling) => SourceType.GetStateOf(8, falling);

    //GetSpreadDelay 这次扩散隔多少刻执行 对应原版 getSpreadDelay
    public virtual int GetSpreadDelay(ServerLevel level, BlockPos pos, FluidState oldState, FluidState newState)
        => GetTickDelay(level);

    //GetLegacyLevel 流体状态反推方块状态的 level 0-15 对应原版 getLegacyLevel
    //0 是源 1-7 是逐级降低的流动水 8 是下落水
    protected static int GetLegacyLevel(FluidState state)
        => state.IsSource ? 0 : 8 - Math.Min(state.Amount, 8) + (state.Falling ? 8 : 0);

    //Tick 流体的计划刻 对应原版 FlowingFluid.tick
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

    //Spread 一次扩散 先试向下 下不去再试向四周 对应原版 spread
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
            //上方三面都是源的地漏不必再向四周流 水会整柱灌下去 对应原版 sourceNeighborCount >= 3
            if (SourceNeighborCount(level, pos) >= 3) SpreadToSides(level, pos, fluidState, state);
            return;
        }
        if (fluidState.IsSource || !IsWaterHole(level, pos, state, belowPos, belowState))
            SpreadToSides(level, pos, fluidState, state);
    }

    //SpreadToSides 向四周扩散 先按落差减档 下落态固定 7 档 对应原版 spreadToSides
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

    //GetNewLiquid 算这一格最终该是哪种流体状态 对应原版 getNewLiquid
    //取四周同族流体的最高档减一次落差 上方直接压下来按满档下落 两格以上相邻源且允许转换时原地升成源
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

    //CanPassThroughWall 两面之间能不能让流体穿过 两个整块的遮挡形状互相挡死 对应原版 canPassThroughWall
    private static bool CanPassThroughWall(Direction direction, BlockState sourceState, BlockState targetState)
    {
        var targetShape = OcclusionOf(targetState);
        if (ReferenceEquals(targetShape, Shapes.Block())) return false;
        var sourceShape = OcclusionOf(sourceState);
        if (ReferenceEquals(sourceShape, Shapes.Block())) return false;
        if (ReferenceEquals(sourceShape, Shapes.Empty()) && ReferenceEquals(targetShape, Shapes.Empty())) return true;
        return !Shapes.MergedFaceOccludes(sourceShape, targetShape, direction);
    }

    //OcclusionOf 该状态的遮挡形状 不遮挡光线的方块按空算
    //与 Block.SolidRender 同一套语义 水与岩浆 CanOcclude 为假 不按整块挡住流体自身
    private static VoxelShape OcclusionOf(BlockState state)
        => state.Owner.CanOcclude ? state.Owner.GetOcclusionShape(state) : Shapes.Empty();

    //SpreadTo 把流体写进目标格 对应原版 spreadTo
    protected virtual void SpreadTo(ServerLevel level, BlockPos pos, BlockState state, Direction direction, FluidState target)
    {
        if (!state.Owner.IsAir) BeforeDestroyingBlock(level, pos, state);
        level.SetBlock(pos, target.CreateLegacyBlock(), 3);
    }

    //GetSlopeDistance 沿地形找落点的层数 找到地漏返回当前层 对应原版 getSlopeDistance
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

    //IsWaterHole 该格底下是不是能直接漏下去 对应原版 isWaterHole
    private bool IsWaterHole(ServerLevel level, BlockPos topPos, BlockState topState, BlockPos bottomPos, BlockState bottomState)
    {
        if (!CanPassThroughWall(Direction.Down, topState, bottomState)) return false;
        if (bottomState.FluidState.Type.IsSame(this)) return true;
        return CanHoldFluid(bottomState, FlowingType);
    }

    //IsHole 该格正下方能漏 对应原版 SpreadContext.isHole
    private bool IsHole(ServerLevel level, BlockPos pos)
    {
        var state = StateAt(level, pos);
        return IsWaterHole(level, pos, state, pos.Offset(Direction.Down), StateAt(level, pos.Offset(Direction.Down)));
    }

    //CanPassThrough 这格能不能被该流体穿过去 对应原版 canPassThrough
    private bool CanPassThrough(Fluid fluid, BlockState sourceState, Direction direction, BlockState testState, FluidState testFluidState)
        => CanMaybePassThrough(sourceState, direction, testState, testFluidState)
            && CanHoldSpecificFluid(testState, fluid);

    //CanMaybePassThrough 排除掉同族源格与容纳不了的形状 对应原版 canMaybePassThrough
    private bool CanMaybePassThrough(BlockState sourceState, Direction direction, BlockState testState, FluidState testFluidState)
        => !IsSourceBlockOfThisType(testFluidState)
            && CanHoldAnyFluid(testState)
            && CanPassThroughWall(direction, sourceState, testState);

    //IsSourceBlockOfThisType 该状态是不是本流体的源 对应原版 isSourceBlockOfThisType
    private bool IsSourceBlockOfThisType(FluidState state)
        => state.Type.IsSame(this) && state.IsSource;

    //SourceNeighborCount 四周有几个同族源 对应原版 sourceNeighborCount
    private int SourceNeighborCount(ServerLevel level, BlockPos pos)
    {
        var count = 0;
        foreach (var direction in HorizontalDirections)
            if (IsSourceBlockOfThisType(level.GetFluidState(pos.Offset(direction)))) count++;
        return count;
    }

    //GetSpread 算出四周各自该被填成什么状态 落点越近的优先 对应原版 getSpread
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

    //CanHoldAnyFluid 该方块能不能装下任意流体 门 告示牌 梯子 甘蔗 传送门一类不算可容纳 对应原版 canHoldAnyFluid
    private static bool CanHoldAnyFluid(BlockState state)
    {
        var block = state.Owner;
        if (block is BlockBehaviour behaviour && behaviour.HasCollision) return false;
        var path = block.Id.Path;
        return path is not ("ladder" or "sugar_cane" or "bubble_column" or "nether_portal"
            or "end_portal" or "end_gateway" or "structure_void")
            && !path.EndsWith("_sign") && !path.EndsWith("_door");
    }

    //CanHoldFluid 该方块能不能装下这一种流体 对应原版 canHoldFluid
    private static bool CanHoldFluid(BlockState state, Fluid fluid)
        => CanHoldAnyFluid(state) && CanHoldSpecificFluid(state, fluid);

    //CanHoldSpecificFluid 该方块对这种流体有没有额外限制
    //原版问的是 LiquidBlockContainer 气泡柱与海带那类方块 本作还没有实现者 恒真
    private static bool CanHoldSpecificFluid(BlockState state, Fluid fluid) => true;

    //CanBeReplacedAt 问某格现有流体能不能被顶掉 对应原版 FluidState.canBeReplacedWith
    //空流体总能被顶掉 原版走 EmptyFluid 的默认实现 本作 EmptyFluid 在注册表层挂不上行为接口 这里短路
    private static bool CanBeReplacedAt(FluidState state, ServerLevel level, BlockPos pos, Fluid other, Direction direction)
        => state.IsEmpty
            || (state.Type is IFluidBehaviour behaviour && behaviour.CanBeReplacedWith(state, level, pos, other, direction));

    //IsSolid 该方块状态算不算固体 水源生成判定用它
    private static bool IsSolid(BlockState state)
        => state.Owner is BlockBehaviour behaviour && behaviour.HasCollision;

    //StateAt 读方块状态 区块不在内存时按空气处理 流体刻不会发生在未加载位置
    private static BlockState StateAt(ServerLevel level, BlockPos pos)
        => level.GetBlockState(pos) ?? Blocks.AIR.DefaultBlockState;

    //HasSameAbove 上方压着同族流体 对应原版 hasSameAbove
    protected static bool HasSameAbove(FluidState fluidState, ServerLevel level, BlockPos pos)
        => fluidState.Type.IsSame(level.GetFluidState(pos.Offset(Direction.Up)).Type);
}
