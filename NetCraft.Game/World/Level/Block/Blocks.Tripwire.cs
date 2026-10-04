using NetCraft.Game.Server;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Ticks;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//绊线系列 对应原版 net.minecraft.world.level.block.TripWireHookBlock 与 TripWireBlock
//绊线钩挂在墙上当锚点 绊线在两点之间拉一条 有实体踩上去就让钩子输出 15 强度
//两者互相调用算状态 钩子负责扫整条线 线负责在自己被踩时通知钩子
public static partial class Blocks
{
    public static readonly TripWireHookBlock TRIPWIRE_HOOK = new();
    public static readonly TripWireBlock TRIPWIRE = new();

    //RegisterTripwire 绊线系列登记进真实方块表
    private static void RegisterTripwire(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { TRIPWIRE_HOOK, TRIPWIRE };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //TripWireHookBlock 绊线钩 对应原版 TripWireHookBlock
    //扫描朝向那一侧最多 42 格 另一端也有个正对的钩子才算连上 连上后按线上是否被踩决定通电
    public sealed class TripWireHookBlock : BlockBehaviour
    {
        //WireDistMax 一条绊线的最大长度 对应原版 WIRE_DIST_MAX
        public const int WireDistMax = 42;

        //RecheckPeriod 被踩住期间重新盘查的间隔 对应原版 RECHECK_PERIOD
        public const int RecheckPeriod = 10;

        //钩子形状 六像素宽十像素高 贴在朝向的反面 对应原版 SHAPES
        private static readonly Dictionary<Direction, VoxelShape> HookShapes =
            Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(6.0, 0.0, 10.0, 10.0, 16.0));

        public override Identifier Id => Identifier.WithDefaultNamespace("tripwire_hook");

        //原版绊线钩硬度 0 一碰就碎
        public override float DestroySpeed => 0f;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => HookShapes[state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];

        //CanSurvive 朝向的反面要有坚固面托着 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var behind = pos.Offset(direction.Opposite);
            return level.GetBlockState(behind) is { } support
                && support.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, behind, support, direction);
        }

        //UpdateShape 托着的方块没了就掉 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            var facing = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            if (directionToNeighbour == facing.Opposite && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        //GetStateForPlacement 朝向取玩家水平朝向的反面 贴不住就放不下 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
        {
            var state = DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing, horizontalFacing.Opposite.ToState());
            return CanSurvive(level, pos, state) ? state : null;
        }

        //SetPlacedBy 放下时盘查一次整条线 对应原版 setPlacedBy
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
            => CalculateState(level, pos, state, false, false, -1, null);

        //Tick 定期重新盘查 线被踩住期间靠它维持 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
            => CalculateState(level, pos, state, false, true, -1, null);

        //AffectNeighborsAfterRemoval 拆钩子前先让整条线知道 对应原版 affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            OnRemoved(level, pos, state);
        }

        public override bool IsSignalSource => true;

        //OwnSignal 通电时给满 对应原版 ownSignal
        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        //GetDirectSignal 只朝朝向那一面给 对应原版 getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.Powered)
                && state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction
                ? 15
                : 0;

        //CalculateState 从钩子出发扫整条线重算 attached 与 powered 对应原版同名方法
        //isBeingDestroyed 为真表示钩子正在被拆 此时不再把自己写回去
        //wireSource 是本次触发来自线上第几格 那一格用传入的状态参与计算而不是从世界读
        public static void CalculateState(ServerLevel level, BlockPos pos, BlockState state,
            bool isBeingDestroyed, bool canUpdate, int wireSource, BlockState? wireSourceState)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var wasAttached = state.GetValue(BlockStateProperties.Attached);
            var wasPowered = state.GetValue(BlockStateProperties.Powered);
            var attached = !isBeingDestroyed;
            var powered = false;
            var receiverPos = 0;
            var wireStates = new BlockState?[WireDistMax];
            for (var i = 1; i < WireDistMax; i++)
            {
                var testPos = pos.Relative(direction, i);
                var wireState = level.GetBlockState(testPos);
                if (wireState is not { } wire)
                {
                    wireStates[i] = null;
                    attached = false;
                    continue;
                }
                //另一端也有个正对着的钩子才算连成一条
                if (ReferenceEquals(wire.Owner, TRIPWIRE_HOOK))
                {
                    if (wire.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() != direction.Opposite) break;
                    receiverPos = i;
                    break;
                }
                if (!ReferenceEquals(wire.Owner, TRIPWIRE) && i != wireSource)
                {
                    wireStates[i] = null;
                    attached = false;
                    continue;
                }
                if (i == wireSource && wireSourceState is { } source) wire = source;
                var armed = !wire.GetValue(BlockStateProperties.Disarmed);
                powered |= armed && wire.GetValue(BlockStateProperties.Powered);
                wireStates[i] = wire;
                if (i == wireSource)
                {
                    level.ScheduleTick(pos, state.Owner, RecheckPeriod);
                    attached &= armed;
                }
            }
            //两端都连着才算连上 只有一端绷不直 对应原版 attached &= receiverPos > 1
            attached &= receiverPos > 1;
            powered &= attached;
            var newState = state.TrySetValue(BlockStateProperties.Attached, attached)
                .TrySetValue(BlockStateProperties.Powered, powered);

            if (receiverPos > 0)
            {
                var receiverBlockPos = pos.Relative(direction, receiverPos);
                var opposite = direction.Opposite;
                level.SetBlock(receiverBlockPos, newState.SetValue(BlockStateProperties.HorizontalFacing,
                    opposite.ToState()), BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
                level.UpdateNeighborsAt(receiverBlockPos, state.Owner);
                level.UpdateNeighborsAt(receiverBlockPos.Offset(opposite), state.Owner);
                if (level.GetBlockState(pos) is not { } current || !ReferenceEquals(current.Owner, TRIPWIRE_HOOK))
                {
                    OnRemoved(level, pos, newState);
                    return;
                }
            }

            if (!isBeingDestroyed)
            {
                level.SetBlock(pos, newState.SetValue(BlockStateProperties.HorizontalFacing, direction.ToState()),
                    BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
                if (canUpdate)
                {
                    level.UpdateNeighborsAt(pos, state.Owner);
                    level.UpdateNeighborsAt(pos.Offset(direction.Opposite), state.Owner);
                }
            }

            //连上与否变了就把整条线的 attached 一起改掉 线才知道自己被绷直了
            if (wasAttached == attached) return;
            for (var i = 1; i < receiverPos; i++)
            {
                if (wireStates[i] is not { } wireData) continue;
                var testPos = pos.Relative(direction, i);
                if (level.GetBlockState(testPos) is not { } testPosState) continue;
                if (!ReferenceEquals(testPosState.Owner, TRIPWIRE)
                    && !ReferenceEquals(testPosState.Owner, TRIPWIRE_HOOK)) continue;
                level.SetBlock(testPos, wireData.TrySetValue(BlockStateProperties.Attached, attached),
                    BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            }
        }

        //OnRemoved 钩子被拆时的收尾 对应原版 onRemoved
        //原版这里还播拆解音效 方块行为拿不到音效出口 与拉杆按钮一并留到音效出口开出来
        private static void OnRemoved(ServerLevel level, BlockPos pos, BlockState state)
        {
            var attached = state.GetValue(BlockStateProperties.Attached);
            var powered = state.GetValue(BlockStateProperties.Powered);
            if (attached || powered) CalculateState(level, pos, state, true, false, -1, null);
            if (!powered) return;
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            level.UpdateNeighborsAt(pos, state.Owner);
            level.UpdateNeighborsAt(pos.Offset(direction.Opposite), state.Owner);
        }
    }

    //TripWireBlock 绊线 对应原版 TripWireBlock
    //两端连着钩子才算绷直(attached) 有实体踩在线上就置 powered 并通知钩子
    public sealed class TripWireBlock : BlockBehaviour
    {
        //被踩住期间重新盘查的间隔 对应原版 RECHECK_PERIOD
        private const int RecheckPeriod = 10;

        //绷直与未绷直两种形状 绷直时线拉紧贴在自己那格下方 对应原版 SHAPE_ATTACHED/SHAPE_NOT_ATTACHED
        private static readonly VoxelShape AttachedShape = NetCraft.Registry.Block.Column(16.0, 1.0, 2.5);
        private static readonly VoxelShape DroppedShape = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);

        //实体检测盒 与上面两个形状一一对应 像素换格 对应原版 checkPressed 里用的形状包围盒
        private static readonly AABB AttachedBox = new(0.0, 1 / 16.0, 0.0, 1.0, 2.5 / 16.0, 1.0);
        private static readonly AABB DroppedBox = new(0.0, 0.0, 0.0, 1.0, 0.5, 1.0);

        public override Identifier Id => Identifier.WithDefaultNamespace("tripwire");

        //原版绊线硬度 0 一碰就碎
        public override float DestroySpeed => 0f;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Attached) ? AttachedShape : DroppedShape;

        //GetStateForPlacement 按四邻算出四向连接 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState
                .SetValue(BlockStateProperties.NorthConnected, Connects(level, pos, Direction.North))
                .SetValue(BlockStateProperties.EastConnected, Connects(level, pos, Direction.East))
                .SetValue(BlockStateProperties.SouthConnected, Connects(level, pos, Direction.South))
                .SetValue(BlockStateProperties.WestConnected, Connects(level, pos, Direction.West));

        //UpdateShape 水平邻居变了就重算那一向的连接 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (!directionToNeighbour.IsHorizontal) return state;
            var property = PropertyFor(directionToNeighbour);
            return state.SetValue(property, ShouldConnectTo(neighbourState, directionToNeighbour));
        }

        //OnPlace 线刚放好先让两端钩子盘查一次 对应原版 onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdateSource(level, pos, state);
        }

        //AffectNeighborsAfterRemoval 线被拆时会漏气 通知两端钩子重算 对应原版 affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            UpdateSource(level, pos, state.SetValue(BlockStateProperties.Powered, true));
        }

        //PlayerDestroy 手持剪刀拆线时先把这一格标成已剪断 对应原版 playerWillDestroy
        //破坏是在本回调之后才发生的 所以同刻钩子重算时读到的还是这一格 DISARMED=true
        //原版这里还发 GameEventSHEAR 本作没有游戏事件通道 表现上以音效出口为准
        public override void PlayerDestroy(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state)
        {
            var held = player.Inventory.GetSelectedItem();
            if (held.IsEmpty() || held.GetItem().Id.Path != "shears") return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Disarmed, true),
                BlockUpdateFlags.SkipBlockEntitySideEffects | BlockUpdateFlags.Invisible);
        }

        //Tick 被踩住期间定期复查 实体走开就松开 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) return;
            CheckPressed(level, pos, state);
        }

        //OnEntityInside 有实体进到线所在格立即复查 对应原版 entityInside
        //原版把踩上来的实体直接传进来 本作的钩子只给出位置 所以统一按形状范围查实体
        public override void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.GetValue(BlockStateProperties.Powered) || level.HasScheduledTick(pos, this)) return;
            CheckPressed(level, pos, state);
        }

        //ShouldConnectTo 该方块能不能跟绊线对接 对应原版同名方法
        //另一端绊线钩要正对着自己 朝相反方向
        public bool ShouldConnectTo(BlockState? other, Direction direction)
        {
            if (other is not { } state) return false;
            if (ReferenceEquals(state.Owner, TRIPWIRE_HOOK))
                return state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction.Opposite;
            return ReferenceEquals(state.Owner, TRIPWIRE);
        }

        //PropertyFor 方向到连接属性的映射 只处理水平四向
        //方向是结构体不是枚举 不能用 switch 的模式匹配 只能逐项比
        private static BooleanProperty PropertyFor(Direction direction)
        {
            if (direction == Direction.North) return BlockStateProperties.NorthConnected;
            if (direction == Direction.East) return BlockStateProperties.EastConnected;
            if (direction == Direction.South) return BlockStateProperties.SouthConnected;
            return BlockStateProperties.WestConnected;
        }

        //Connects 读邻居方块判断该方向是否相连
        private static bool Connects(ServerLevel level, BlockPos pos, Direction direction)
        {
            var neighbourPos = pos.Offset(direction);
            var neighbour = level.GetBlockState(neighbourPos);
            if (neighbour is not { } state) return false;
            if (ReferenceEquals(state.Owner, TRIPWIRE_HOOK))
                return state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction.Opposite;
            return ReferenceEquals(state.Owner, TRIPWIRE);
        }

        //CheckPressed 按形状范围内的实体重判通电与否 变了自己写回并让两端钩子重算
        private void CheckPressed(ServerLevel level, BlockPos pos, BlockState state)
        {
            var template = state.GetValue(BlockStateProperties.Attached) ? AttachedBox : DroppedBox;
            var box = template.Move(new Vec3(pos.X, pos.Y, pos.Z));
            var shouldBePressed = level.CountEntitiesInBox(box) > 0;
            var wasPressed = state.GetValue(BlockStateProperties.Powered);
            if (shouldBePressed != wasPressed)
            {
                var newState = state.SetValue(BlockStateProperties.Powered, shouldBePressed);
                level.SetBlock(pos, newState, BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
                UpdateSource(level, pos, newState);
            }
            if (shouldBePressed) level.ScheduleTick(pos, this, RecheckPeriod);
            else if (wasPressed) level.ScheduleTick(pos, this, 0);
        }

        //UpdateSource 朝南与朝西两个方向各扫一遍 撞到钩子就让它重算整条线 对应原版 updateSource
        //只扫两个方向是因为另外两个方向必然从对侧被扫到 两个方向就够覆盖整条线
        private void UpdateSource(ServerLevel level, BlockPos pos, BlockState state)
        {
            foreach (var direction in new[] { Direction.South, Direction.West })
            {
                for (var i = 1; i < TripWireHookBlock.WireDistMax; i++)
                {
                    var testPos = pos.Relative(direction, i);
                    if (level.GetBlockState(testPos) is not { } block) break;
                    if (ReferenceEquals(block.Owner, TRIPWIRE_HOOK))
                    {
                        if (block.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()
                            != direction.Opposite) break;
                        TripWireHookBlock.CalculateState(level, testPos, block, false, true, i, state);
                        break;
                    }
                    if (!ReferenceEquals(block.Owner, TRIPWIRE)) break;
                }
            }
        }
    }
}
