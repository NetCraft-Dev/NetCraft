using System.Runtime.CompilerServices;
using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Logging;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Redstone;
using NetCraft.Storage.Ticks;
using NetCraft.Storage.Updates;
using NetCraft.Util.Random;
using AttachFace = NetCraft.Registry.Enums.AttachFace;
using ComparatorMode = NetCraft.Registry.Enums.ComparatorMode;
using StateDirection = NetCraft.Registry.Enums.Direction;
//Util 下另有 Random 命名空间与 System.Random 撞名 只取 Mth
using Mth = NetCraft.Util.Mth;

namespace NetCraft.Game.World.Level.Block;

//Blocks 红石元件部分 与 Blocks.cs 同一个类分开文件免得主文件太长
//属性名与值序一律照 blocks.txt 那份走 属性错位会让全局 BlockState id 跟着错
public static partial class Blocks
{
    //RedstoneBlock 红石块 恒定 15 强度信号源 没有属性
    public sealed class RedstoneBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_block");
        //原版红石块硬度 5 需要镐子
        public override float DestroySpeed => 5f;
        public override bool RequiresCorrectToolForDrops => true;
        public override bool IsSignalSource => true;

        //红石块任何方向都给满 15 对应原版 getSignal
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction) => 15;

        //红石块不充当直接信号源 方块的直接信号由本体给 对应原版没有覆写 getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => 0;
    }

    //FaceAttachedHorizontalDirectionalBlock 贴面方块基类 拉杆与按钮共用 对应原版同名类
    //FACE 决定贴地贴顶还是贴墙 FACING 是水平朝向 贴墙时它指向背离支撑的那一侧
    public abstract class FaceAttachedHorizontalDirectionalBlock : BlockBehaviour
    {
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["face"] = BlockStateProperties.AttachFaceProperty,
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["powered"] = BlockStateProperties.Powered,
        };

        //贴面方块都不是导体 信号只走直接信号那条
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        //GetDirectSignal 通电时只朝附着方向那一侧给 对应原版 getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.Powered) && GetConnectedDirection(state) == direction ? 15 : 0;

        //原版构造器显式指定默认态 不指定的话会落到 face=floor 那个组合上
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Powered, false)
                .SetValue(BlockStateProperties.AttachFaceProperty, AttachFace.wall);

        //GetStateForPlacement 点到顶面立在地面 点到天花板挂在上面 点到侧面贴墙 对应原版同名方法
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
        {
            var state = face == Direction.Up
                ? DefaultBlockState
                    .SetValue(BlockStateProperties.AttachFaceProperty, AttachFace.floor)
                    .SetValue(BlockStateProperties.HorizontalFacing, horizontalFacing.ToState())
                : face == Direction.Down
                    ? DefaultBlockState
                        .SetValue(BlockStateProperties.AttachFaceProperty, AttachFace.ceiling)
                        .SetValue(BlockStateProperties.HorizontalFacing, horizontalFacing.ToState())
                    //贴墙时 FACING 指向外侧 也就是玩家点到的那一面
                    : DefaultBlockState
                        .SetValue(BlockStateProperties.AttachFaceProperty, AttachFace.wall)
                        .SetValue(BlockStateProperties.HorizontalFacing, face.ToState());
            return CanSurvive(level, pos, state) ? state : null;
        }

        //UpdateShape 依附的方块没了就整块掉 对应原版同名方法
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (GetConnectedDirection(state).Opposite == directionToNeighbour && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var connected = GetConnectedDirection(state);
            var supportPos = pos.Offset(connected.Opposite);
            var support = level.GetBlockState(supportPos);
            return support is not null && support.Value.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, supportPos, support.Value, connected);
        }

        //GetConnectedDirection 靠哪一面附着 对应原版 getConnectedDirection
        public static Direction GetConnectedDirection(BlockState state)
            => state.GetValue(BlockStateProperties.AttachFaceProperty) switch
            {
                AttachFace.ceiling => Direction.Down,
                AttachFace.floor => Direction.Up,
                _ => state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive(),
            };

        //RotateAttachFace 由北向形状转出贴面三态 × 水平四向 对应原版 Shapes.rotateAttachFace
        //AttachFace 在 Registry 层 Shapes 在 Primitives 层 转不动才落在 Game 层的贴面基类上
        protected static Dictionary<AttachFace, Dictionary<Direction, VoxelShape>> RotateAttachFace(VoxelShape north)
            => new()
            {
                [AttachFace.wall] = Shapes.RotateHorizontal(north),
                [AttachFace.floor] = Shapes.RotateHorizontal(north, OctahedralGroups.BlockRotX270),
                [AttachFace.ceiling] = Shapes.RotateHorizontal(north,
                    OctahedralGroups.BlockRotY180.Compose(OctahedralGroups.BlockRotX90)),
            };

        //UpdateNeighbours 本体与附着侧那格各通知一次 对应原版 updateNeighbours
        protected void UpdateNeighbours(ServerLevel level, BlockPos pos, BlockState state)
        {
            var front = GetConnectedDirection(state).Opposite;
            level.UpdateNeighborsAt(pos, this);
            level.UpdateNeighborsAt(pos.Offset(front), this);
        }
    }

    //LeverBlock 拉杆 右键切换通电 对应原版 LeverBlock
    public sealed class LeverBlock : FaceAttachedHorizontalDirectionalBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("lever");

        //拉杆形状 北向定为沿 Z 从 10 到 16 的细长块 对应原版 makeShapes
        //缺了它形状落到默认整格 天光被整格挡掉 拉杆那格会比原版暗
        private static readonly Dictionary<AttachFace, Dictionary<NetCraft.Primitives.Direction, VoxelShape>>
            AttachShapes = RotateAttachFace(NetCraft.Registry.Block.BoxZ(6.0, 8.0, 10.0, 16.0));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => AttachShapes[state.GetValue(BlockStateProperties.AttachFaceProperty)]
                [state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];

        //原版拉杆硬度 0.5 空手可挖
        public override float DestroySpeed => 0.5f;

        //UseOn 拉杆不吃物品 右键直接切换 对应原版 useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            Pull(level, pos, state);
            return true;
        }

        //Pull 切换通电态并通知邻接 对应原版 pull
        //原版那个重载还带 player 与 gameEvent 前者只影响声音投递对象 后者这套机制本作还没有
        public void Pull(ServerLevel level, BlockPos pos, BlockState state)
        {
            var newState = state.Cycle(BlockStateProperties.Powered);
            level.SetBlock(pos, newState, BlockUpdateFlags.All);
            UpdateNeighbours(level, pos, newState);
            //原版扳动有咔哒声 音高通电 0.6 断电 0.5
            level.PlaySound(SoundEvents.LeverClick, SoundSource.Blocks, pos, 0.3f,
                newState.GetValue(BlockStateProperties.Powered) ? 0.6f : 0.5f);
        }

        //AffectNeighborsAfterRemoval 通电的拉杆被拆掉要让邻接重算 对应原版同名方法
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston && state.GetValue(BlockStateProperties.Powered))
                UpdateNeighbours(level, pos, state);
        }
    }

    //ButtonBlock 按钮 右键按下 到点自动弹起 对应原版 ButtonBlock
    //ticksToStayPressed 石制 20 刻木制 30 刻 由注册处给
    public sealed class ButtonBlock : FaceAttachedHorizontalDirectionalBlock
    {
        private readonly string _name;
        private readonly int _ticksToStayPressed;

        public ButtonBlock(string name, int ticksToStayPressed)
        {
            _name = name;
            _ticksToStayPressed = ticksToStayPressed;
        }

        //贴面托座 北向定为沿 Z 从中心到 16 的薄块 对应原版 makeShapes 的 attachFace
        private static readonly Dictionary<AttachFace, Dictionary<NetCraft.Primitives.Direction, VoxelShape>>
            AttachShapes = RotateAttachFace(NetCraft.Registry.Block.BoxZ(6.0, 4.0, 8.0, 16.0));
        //按下时按钮主体 14 像素 未按下 12 像素 对应原版 pressedShaper/unpressedShaper
        private static readonly VoxelShape PressedCube = NetCraft.Registry.Block.Cube(14.0);
        private static readonly VoxelShape UnpressedCube = NetCraft.Registry.Block.Cube(12.0);

        //GetShape 贴面托座与按钮主体取并集外的托座部分 对应原版 Shapes.join(..., ONLY_FIRST)
        //缺了它形状落到默认整格 天光被整格挡掉 按钮那格会比原版暗
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
        {
            var support = AttachShapes[state.GetValue(BlockStateProperties.AttachFaceProperty)]
                [state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];
            var core = state.GetValue(BlockStateProperties.Powered) ? PressedCube : UnpressedCube;
            return Shapes.Join(support, core, BooleanOps.OnlyFirst);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //原版按钮硬度 0.5 空手可挖
        public override float DestroySpeed => 0.5f;

        //UseOn 已按下时不重复触发 原版这里返回 CONSUME 同样是拦住放置
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) Press(level, pos, state);
            return true;
        }

        //Press 按下并排一刻到点弹起 对应原版 press
        public void Press(ServerLevel level, BlockPos pos, BlockState state)
        {
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, true),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            UpdateNeighbours(level, pos, state);
            level.ScheduleTick(pos, this, _ticksToStayPressed);
        }

        //Tick 到点重判是否还按着 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Powered)) CheckPressed(level, pos, state);
        }

        //OnEntityInside 有实体碰到时立即重判 对应原版 entityInside
        public override void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) CheckPressed(level, pos, state);
        }

        //AffectNeighborsAfterRemoval 按下的按钮被拆掉要让邻接重算
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston && state.GetValue(BlockStateProperties.Powered))
                UpdateNeighbours(level, pos, state);
        }

        //CheckPressed 重判按下状态 对应原版 checkPressed
        //原版在这里找碰撞形状范围内的箭 本作没有箭实体也没有形状系统 等效于恒无箭
        //因此只有石制按钮会被箭激活那条分支暂时无法触发 木制按钮本来就只认右键
        private void CheckPressed(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, false),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            UpdateNeighbours(level, pos, state);
        }
    }

    //RedstoneTorchBlock 红石火把 立在地面 下方有信号就熄灭 频闪过度会烧毁
    public class RedstoneTorchBlock : BlockBehaviour
    {
        //REDSTONE_TORCH_BURNOUT 烧毁的世界事件 id 对应原版 LevelEvent.REDSTONE_TORCH_BURNOUT
        public const int BurnoutEvent = 1502;
        //RecentToggleWindow 统计窗口 60 刻 对应原版 RECENT_TOGGLE_TIMER
        public const int RecentToggleWindow = 60;
        //MaxRecentToggles 窗口内切换到这个数就烧毁 对应原版 MAX_RECENT_TOGGLES
        public const int MaxRecentToggles = 8;
        //RestartDelay 烧毁后重新点亮的延迟 对应原版 RESTART_DELAY
        public const int RestartDelay = 160;
        //ToggleDelay 邻居变化到重新判定的延迟 对应原版 TOGGLE_DELAY
        public const int ToggleDelay = 2;

        //_recentToggles 近期的切换记录 按关卡弱挂 关卡回收记录一起走 对应原版 RECENT_TOGGLES
        private static readonly ConditionalWeakTable<ServerLevel, List<ToggleEntry>> RecentToggles = new();

        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_torch");

        //火把形状 4 像素宽 10 像素高 对应原版 BaseTorchBlock.SHAPE
        //缺了它形状落到默认整格 天光被整格挡掉 火把那格会比原版暗
        private static readonly VoxelShape TorchShape = NetCraft.Registry.Block.Column(4.0, 0.0, 10.0);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => TorchShape;

        //原版火把硬度 0 一碰就碎
        public override float DestroySpeed => 0f;
        public override int LightEmission => 7;

        //火把不是导体 信号走直接信号那条
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["lit"] = BlockStateProperties.Lit };

        //原版火把默认是亮的
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0].SetValue(BlockStateProperties.Lit, true);

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Lit) ? 15 : 0;

        //GetSignal 火把不朝上供电 其余方向给自身强度 对应原版 getSignal
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => direction == Direction.Up ? 0 : OwnSignal(level, pos, state);

        //GetDirectSignal 只有朝下那一路算直接信号 对应原版 getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => direction == Direction.Down ? GetSignal(level, pos, state, direction) : 0;

        //OnPlace 放下时通知六向 对应原版 onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston) => NotifyNeighbors(level, pos);

        //AffectNeighborsAfterRemoval 拆掉时同样要通知 未被活塞推动才算 对应原版同名方法
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston) NotifyNeighbors(level, pos);
        }

        //NeighborChanged 亮灭状态与下方信号不一致就排一刻重判 对应原版 neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            if (state.GetValue(BlockStateProperties.Lit) == HasNeighborSignal(level, pos, state)
                && !level.WillTickThisTick(pos, this))
                level.ScheduleTick(pos, this, ToggleDelay);
        }

        //Tick 亮着且下方有信号就熄 熄着且下方无信号就亮 频闪过快烧毁 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            var neighborSignal = HasNeighborSignal(level, pos, state);
            var toggles = RecentToggles.GetOrCreateValue(level);
            toggles.RemoveAll(t => level.GameTime - t.When > RecentToggleWindow);

            if (state.GetValue(BlockStateProperties.Lit))
            {
                if (!neighborSignal) return;
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Lit, false),
                    BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
                if (!IsToggledTooFrequently(level, pos, true)) return;
                //烧毁只发事件与延长重判 方块本身留在熄灭态
                level.LevelEvent(BurnoutEvent, pos, 0);
                level.ScheduleTick(pos, this, RestartDelay);
                return;
            }

            if (!neighborSignal && !IsToggledTooFrequently(level, pos, false))
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Lit, true),
                    BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //HasNeighborSignal 只看下方那一格的信号 对应原版同名方法
        protected virtual bool HasNeighborSignal(ServerLevel level, BlockPos pos, BlockState state)
            => level.HasSignal(pos.Offset(Direction.Down), Direction.Down);

        //UpdateShape 下方支撑没了就掉 对应原版 BaseTorchBlock.updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Down && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
            => IsSupportSturdy(level, pos.Offset(Direction.Down), Direction.Up);

        //NotifyNeighbors 六向各通知一次 对应原版 notifyNeighbors
        private void NotifyNeighbors(ServerLevel level, BlockPos pos)
        {
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //IsToggledTooFrequently 窗口内同位置切换次数是否超限 add 为真时先记一笔 对应原版同名方法
        private static bool IsToggledTooFrequently(ServerLevel level, BlockPos pos, bool add)
        {
            var toggles = RecentToggles.GetOrCreateValue(level);
            if (add) toggles.Add(new ToggleEntry(pos, level.GameTime));
            var count = 0;
            foreach (var toggle in toggles)
            {
                if (toggle.Pos != pos) continue;
                if (++count >= MaxRecentToggles) return true;
            }
            return false;
        }

        //IsSupportSturdy 支撑面是否够坚固 无形状系统时用整格实心近似
        protected static bool IsSupportSturdy(ServerLevel level, BlockPos supportPos, Direction directionToSupport)
        {
            var support = level.GetBlockState(supportPos);
            return support is not null && support.Value.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, supportPos, support.Value, directionToSupport);
        }

        //ToggleEntry 一条切换记录 对应原版 RedstoneTorchBlock.Toggle
        private readonly record struct ToggleEntry(BlockPos Pos, long When);
    }

    //RedstoneWallTorchBlock 墙上的红石火把 亮灭判定换成看附着面那一侧
    public sealed class RedstoneWallTorchBlock : RedstoneTorchBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_wall_torch");

        //墙火把形状按朝向取 对应原版 WallTorchBlock.SHAPES
        private static readonly Dictionary<NetCraft.Primitives.Direction, VoxelShape> WallShapes =
            Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(5.0, 3.0, 13.0, 11.0, 16.0));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => WallShapes[state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["lit"] = BlockStateProperties.Lit,
        };

        //原版墙火把默认态是 FACING=north LIT=true
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Lit, true);

        //点到侧面才落这个方块 朝向就是被点到的那一面 对应原版 WallTorchBlock.getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
        {
            if (!face.IsHorizontal) return null;
            var state = DefaultBlockState.SetValue(BlockStateProperties.HorizontalFacing, face.ToState());
            return CanSurvive(level, pos, state) ? state : null;
        }

        //HasNeighborSignal 看贴着的那一面外侧的信号 对应原版同名覆写
        protected override bool HasNeighborSignal(ServerLevel level, BlockPos pos, BlockState state)
        {
            var back = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive().Opposite;
            return level.HasSignal(pos.Offset(back), back);
        }

        //GetSignal 不朝贴着的那一面输出 对应原版同名覆写
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction
                ? 0
                : OwnSignal(level, pos, state);

        //UpdateShape 依附的墙没了就掉 对应原版同名覆写
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            var back = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive().Opposite;
            if (directionToNeighbour == back && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var facing = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            return IsSupportSturdy(level, pos.Offset(facing.Opposite), facing);
        }
    }

    //BasePressurePlateBlock 压力板基类 对应原版 BasePressurePlateBlock
    //按下后每 PressedTime 刻重算一次 实体进入时只在未按下时立即重算
    public abstract class BasePressurePlateBlock : BlockBehaviour
    {
        //PressedTime 按下后的重算间隔 简单板 20 刻 测重板 10 刻
        protected virtual int PressedTime => 20;

        protected abstract int GetSignalForState(BlockState state);

        protected abstract BlockState SetSignalForState(BlockState state, int signal);

        protected abstract int GetSignalStrength(ServerLevel level, BlockPos pos);

        //形状 未按下 1 像素厚 按下 0.5 像素 对应原版 SHAPE/SHAPE_PRESSED
        //缺了它形状落到默认整格 天光被整格挡掉 压力板那格会比原版暗
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 1.0);
        private static readonly VoxelShape ShapePressed = NetCraft.Registry.Block.Column(14.0, 0.0, 0.5);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => GetSignalForState(state) > 0 ? ShapePressed : Shape;

        //TouchBox 实体检测盒 与原版 TOUCH_AABB 一致 底面内缩一格宽 4 像素高
        protected static AABB TouchBox(BlockPos pos) => new(
            pos.X + 1 / 16.0, pos.Y, pos.Z + 1 / 16.0,
            pos.X + 15 / 16.0, pos.Y + 4 / 16.0, pos.Z + 15 / 16.0);

        //压力板不是导体 信号只朝上给
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => GetSignalForState(state);

        //GetDirectSignal 只朝上给 对应原版 getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => direction == Direction.Up ? GetSignalForState(state) : 0;

        //UpdateShape 下方支撑没了就掉 对应原版同名方法
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Down && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            var support = level.GetBlockState(below);
            return support is not null && support.Value.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, below, support.Value, Direction.Up);
        }

        //Tick 按下期间重算 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            var signal = GetSignalForState(state);
            if (signal > 0) CheckPressed(level, pos, state, signal);
        }

        //OnEntityInside 有实体踩上来时重算 已在按下态就不必重算 对应原版 entityInside
        public override void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state)
        {
            var signal = GetSignalForState(state);
            if (signal == 0) CheckPressed(level, pos, state, signal);
        }

        //AffectNeighborsAfterRemoval 按下的压力板被拆掉要让邻接重算
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston && GetSignalForState(state) > 0) UpdateNeighbours(level, pos);
        }

        //CheckPressed 重算信号 变了自己写回并通知邻接 还按着就续排一刻 对应原版 checkPressed
        //写回只用 Clients 位 邻居由 updateNeighbours 手动通知 与原版 flags 2 一致
        private void CheckPressed(ServerLevel level, BlockPos pos, BlockState state, int oldSignal)
        {
            var signal = GetSignalStrength(level, pos);
            if (oldSignal != signal)
            {
                level.SetBlock(pos, SetSignalForState(state, signal), BlockUpdateFlags.Clients);
                UpdateNeighbours(level, pos);
            }
            if (signal > 0) level.ScheduleTick(pos, this, PressedTime);
        }

        //UpdateNeighbours 本体与下方那格各通知一次 对应原版 updateNeighbours
        private void UpdateNeighbours(ServerLevel level, BlockPos pos)
        {
            level.UpdateNeighborsAt(pos, this);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), this);
        }
    }

    //PressurePlateBlock 简单压力板 踩上去就给满 对应原版 PressurePlateBlock
    public sealed class PressurePlateBlock : BasePressurePlateBlock
    {
        private readonly string _name;

        public PressurePlateBlock(string name) => _name = name;

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //原版压力板硬度 0.5 空手可挖
        public override float DestroySpeed => 0.5f;

        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["powered"] = BlockStateProperties.Powered };

        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0].SetValue(BlockStateProperties.Powered, false);

        protected override int GetSignalForState(BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        protected override BlockState SetSignalForState(BlockState state, int signal)
            => state.SetValue(BlockStateProperties.Powered, signal > 0);

        //GetSignalStrength 检测盒里有实体就给满 对应原版按实体种类筛选后的计数
        //原版石头与黑石只认活体 本作实体类型还没有活体标记 先一律按所有实体算
        protected override int GetSignalStrength(ServerLevel level, BlockPos pos)
            => level.CountEntitiesInBox(TouchBox(pos)) > 0 ? 15 : 0;
    }

    //WeightedPressurePlateBlock 测重压力板 信号随实体数量线性上升 对应原版同名类
    public sealed class WeightedPressurePlateBlock : BasePressurePlateBlock
    {
        private readonly string _name;
        private readonly int _maxWeight;

        public WeightedPressurePlateBlock(string name, int maxWeight)
        {
            _name = name;
            _maxWeight = maxWeight;
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        public override float DestroySpeed => 0.5f;

        //测重板每 10 刻重算 与简单板的 20 刻不同 对应原版 getPressedTime
        protected override int PressedTime => 10;

        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["power"] = BlockStateProperties.Power };

        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0].SetValue(BlockStateProperties.Power, 0);

        protected override int GetSignalForState(BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        protected override BlockState SetSignalForState(BlockState state, int signal)
            => state.SetValue(BlockStateProperties.Power, signal);

        //GetSignalStrength 实体数按最大承重取比例再抬到 0-15 对应原版同名方法
        protected override int GetSignalStrength(ServerLevel level, BlockPos pos)
        {
            var count = Math.Min(level.CountEntitiesInBox(TouchBox(pos)), _maxWeight);
            return count <= 0 ? 0 : (int)MathF.Ceiling((float)count / _maxWeight * 15f);
        }
    }

    //DiodeBlock 二极管基类 中继器与比较器共用 对应原版 DiodeBlock
    //信号只朝 FACING 那一侧输出 其余方向一律 0 这是它跟普通信号源的根本差别
    //输入读 FACING 前方 侧向输入读顺逆时针两格 两条输入共同决定翻转
    public abstract class DiodeBlock : BlockBehaviour
    {
        //矮板形状只有 2 像素高 对应原版 DiodeBlock.SHAPE
        //不覆写会落到默认整格 遮挡形状跟着占满 中继器那一格被扣满 15 级光看着全黑
        private static readonly VoxelShape LowShape = NetCraft.Registry.Block.Column(16.0, 0.0, 2.0);

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["powered"] = BlockStateProperties.Powered,
        };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => LowShape;

        //GetDelay 从排刻到真正翻转要等的刻数 中继器按档位走比较器恒 2 对应原版 getDelay
        protected abstract int GetDelay(BlockState state);

        //二极管不是导体 红石线不跟它互连
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsSignalSource => true;
        public override bool IsDiode => true;

        //原版二极管默认态 FACING=north POWERED=false
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Powered, false);

        //GetStateForPlacement 朝玩家看的方向 也就是玩家水平朝向的反向 对应原版同名方法
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
            => DefaultBlockState.SetValue(BlockStateProperties.HorizontalFacing, horizontalFacing.Opposite.ToState());

        //CanSurvive 只看下方那一格能不能当支撑 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            return CanSurviveOn(level, below, level.GetBlockState(below));
        }

        //CanSurviveOn 邻接方块朝上的那面是否够坚固 对应原版 canSurviveOn
        protected static bool CanSurviveOn(ServerLevel level, BlockPos neighbourPos, BlockState? neighbourState)
            => neighbourState?.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, neighbourPos, neighbourState.Value, Direction.Up);

        //UpdateShape 下方支撑没了就整块掉 对应原版中继器与比较器各自那段覆写
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
            => directionToNeighbour == Direction.Down && !CanSurviveOn(level, neighbourPos, neighbourState)
                ? AIR.DefaultBlockState
                : state;

        //IsLocked 是否被侧向输入锁住 锁住期间不响应输入也不翻转 基类不锁 中继器覆写
        public virtual bool IsLocked(ServerLevel level, BlockPos pos, BlockState state) => false;

        //Tick 排刻到点 按当前输入决定翻转 对应原版 tick
        //写回只用 Clients 位 邻居靠 updateNeighboursInFront 那条或下一轮邻居变化补 与原版 flags 2 一致
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (IsLocked(level, pos, state)) return;
            var on = state.GetValue(BlockStateProperties.Powered);
            var shouldTurnOn = ShouldTurnOn(level, pos, state);
            if (on && !shouldTurnOn)
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, false), BlockUpdateFlags.Clients);
            }
            else if (!on)
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, true), BlockUpdateFlags.Clients);
                //到点时输入已经没了 说明这一刻的翻转站不住 再等一轮回判 对应原版这条补排
                if (!shouldTurnOn) level.ScheduleTick(pos, this, GetDelay(state), TickPriority.VeryHigh);
            }
        }

        //NeighborChanged 输入侧或侧向变化时重排刻 支撑没了就掉 对应原版 neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            //原版先确认当前位置还是本方块 已被替换掉就什么都不做
            if (!ReferenceEquals(level.GetBlockState(pos)?.Owner, this)) return;
            if (CanSurvive(level, pos, state))
            {
                CheckTickOnNeighbor(level, pos, state);
                return;
            }
            //支撑没了走完整销毁 掉落与方块实体内容由 Game 层销毁流程处理 对应原版 dropResources + removeBlock
            level.BlockUpdateSink?.DestroyBlock(pos, true, BlockUpdateFlags.UpdateLimitDefault);
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //CheckTickOnNeighbor 当前态与应有态不一致且还没排刻就排一刻 对应原版 checkTickOnNeighbor
        //优先级 前方是反向二极管时最高 正在通电时次高 其余普通
        protected virtual void CheckTickOnNeighbor(ServerLevel level, BlockPos pos, BlockState state)
        {
            var locked = IsLocked(level, pos, state);
            var on = state.GetValue(BlockStateProperties.Powered);
            var shouldTurnOn = ShouldTurnOn(level, pos, state);
            //判定现场是排查红石不动作的核心 输入与侧输入都读一遍看是不是读错了信号
            Log.Debug($"Redstone diode check {pos} {state.Owner.Id}[{state.Id}] powered={on} should={shouldTurnOn} locked={locked} input={GetInputSignal(level, pos, state)} side={GetAlternateSignal(level, pos, state)} ticking={level.WillTickThisTick(pos, this)}");
            if (locked) return;
            if (on == shouldTurnOn || level.WillTickThisTick(pos, this)) return;
            var priority = TickPriority.High;
            if (ShouldPrioritize(level, pos, state)) priority = TickPriority.ExtremelyHigh;
            else if (on) priority = TickPriority.VeryHigh;
            level.ScheduleTick(pos, this, GetDelay(state), priority);
        }

        //ShouldTurnOn 输入侧有没有信号 比较器覆写 对应原版同名方法
        protected virtual bool ShouldTurnOn(ServerLevel level, BlockPos pos, BlockState state)
            => GetInputSignal(level, pos, state) > 0;

        //GetInputSignal 读 FACING 前方那一格的信号 对应原版同名方法
        //前方是红石线时把线自身的功率也并进来 红石线对二极管的直接信号是 0 只读 getSignal 会漏
        protected virtual int GetInputSignal(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var targetPos = pos.Offset(direction);
            var input = level.GetSignal(targetPos, direction);
            if (input >= 15) return input;
            var targetState = level.GetBlockState(targetPos);
            if (targetState is not { } target || target.Owner.Id != RedstoneIds.Wire) return input;
            return Math.Max(input, target.HasProperty(BlockStateProperties.Power)
                ? target.GetValue(BlockStateProperties.Power)
                : 0);
        }

        //GetAlternateSignal 顺逆时针两格的侧向输入取最大 对应原版同名方法
        //传的是从本方块指向邻居的方向 与原版一致
        protected int GetAlternateSignal(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var clockWise = direction.ClockWise;
            var counterClockWise = direction.CounterClockWise;
            var onlyDiodes = SideInputDiodesOnly();
            return Math.Max(
                level.GetControlInputSignal(pos.Offset(clockWise), clockWise, onlyDiodes),
                level.GetControlInputSignal(pos.Offset(counterClockWise), counterClockWise, onlyDiodes));
        }

        //SideInputDiodesOnly 侧向输入是否只认二极管 中继器是 比较器不是 对应原版同名方法
        protected virtual bool SideInputDiodesOnly() => false;

        //OwnSignal 通电时输出自己的输出强度 断电恒 0 对应原版 ownSignal
        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? GetOutputSignal(level, pos, state) : 0;

        //GetOutputSignal 输出强度 二极管默认 15 比较器读方块实体 对应原版同名方法
        protected virtual int GetOutputSignal(ServerLevel level, BlockPos pos, BlockState state) => 15;

        //GetDirectSignal 与自身信号一致 对应原版覆写
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => GetSignal(level, pos, state, direction);

        //GetSignal 只朝 FACING 那一侧输出 对应原版覆写
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction
                ? OwnSignal(level, pos, state)
                : 0;

        //ShouldPrioritize 前方是朝向别处的二极管就让这一刻插队 对应原版同名方法
        //两个二极管对顶时靠它保证近输入端先算 顺序错了会出现少一拍或锁死
        public bool ShouldPrioritize(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive().Opposite;
            var oppositeState = level.GetBlockState(pos.Offset(direction));
            return oppositeState is { } opposite
                && opposite.Owner is IBlockSignalBehaviour { IsDiode: true }
                && opposite.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() != direction;
        }

        //OnPlace 放下后通知输入端那格 对应原版 onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston) => UpdateNeighborsInFront(level, pos, state);

        //AffectNeighborsAfterRemoval 拆掉后同样通知输入端 未被活塞推动才算 对应原版同名方法
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston) UpdateNeighborsInFront(level, pos, state);
        }

        //SetPlacedBy 落位时输入端已经有信号就排一刻 对应原版 setPlacedBy
        //不排的话要等下一次邻居变化 输入端一直不动的话二极管会一直不亮
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
        {
            if (ShouldTurnOn(level, pos, state)) level.ScheduleTick(pos, this, 1);
        }

        //UpdateNeighborsInFront 通知 FACING 反向那一格及其邻接 对应原版 updateNeighborsInFront
        protected void UpdateNeighborsInFront(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var backPos = pos.Offset(direction.Opposite);
            level.NeighborChanged(backPos, this);
            level.UpdateNeighborsAtExceptFromFacing(backPos, this, direction);
        }
    }

    //RepeaterBlock 红石中继器 单向延时二极管 侧向有二极管输入时会被锁住 对应原版 RepeaterBlock
    public sealed class RepeaterBlock : DiodeBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("repeater");

        //原版中继器 instabreak 硬度 0 空手秒破
        public override float DestroySpeed => 0f;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["delay"] = BlockStateProperties.Delay,
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["locked"] = BlockStateProperties.Locked,
            ["powered"] = BlockStateProperties.Powered,
        };

        //原版中继器默认态 FACING=north DELAY=1 LOCKED=false POWERED=false
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Delay, 1)
                .SetValue(BlockStateProperties.Locked, false)
                .SetValue(BlockStateProperties.Powered, false);

        //GetDelay 一档两刻 对应原版 getDelay
        protected override int GetDelay(BlockState state) => state.GetValue(BlockStateProperties.Delay) * 2;

        //侧向输入只认二极管 红石线接到侧面不算数 对应原版 sideInputDiodesOnly
        protected override bool SideInputDiodesOnly() => true;

        //IsLocked 侧向有二极管在供电就锁住 锁住期间不翻转 对应原版覆写
        public override bool IsLocked(ServerLevel level, BlockPos pos, BlockState state)
            => GetAlternateSignal(level, pos, state) > 0;

        //UseOn 右键循环延时档位 对应原版 useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            level.SetBlock(pos, state.Cycle(BlockStateProperties.Delay), BlockUpdateFlags.All);
            return true;
        }

        //GetStateForPlacement 落位时先算一次锁定 对应原版覆写
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
        {
            var state = base.GetStateForPlacement(level, pos, face, horizontalFacing);
            return state?.SetValue(BlockStateProperties.Locked, IsLocked(level, pos, state.Value));
        }

        //UpdateShape 侧面邻居变化时重算锁定 朝向轴上那两格不影响 对应原版覆写
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Down && !CanSurviveOn(level, neighbourPos, neighbourState))
                return AIR.DefaultBlockState;
            var facing = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            if (directionToNeighbour.GetAxis() != facing.GetAxis())
                return state.SetValue(BlockStateProperties.Locked, IsLocked(level, pos, state));
            return state;
        }
    }

    //ComparatorBlock 红石比较器 模拟量二极管 比较模式取输入 减去模式取输入减侧输入 对应原版 ComparatorBlock
    public sealed class ComparatorBlock : DiodeBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("comparator");

        //原版比较器 instabreak 硬度 0
        public override float DestroySpeed => 0f;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["mode"] = BlockStateProperties.ComparatorModeProperty,
            ["powered"] = BlockStateProperties.Powered,
        };

        //原版比较器默认态 FACING=north POWERED=false MODE=compare
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Powered, false)
                .SetValue(BlockStateProperties.ComparatorModeProperty, ComparatorMode.compare);

        //比较器固定两刻 对应原版 getDelay
        protected override int GetDelay(BlockState state) => 2;

        //CreateBlockEntity 比较器的输出值存在方块实体里 对应原版 newBlockEntity
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new ComparatorBlockEntity(pos);

        //HasBlockEntity 比较器带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        //GetOutputSignal 读方块实体里存的上次输出 对应原版覆写
        protected override int GetOutputSignal(ServerLevel level, BlockPos pos, BlockState state)
            => level.GetBlockEntity<ComparatorBlockEntity>(pos)?.OutputSignal ?? 0;

        //ShouldTurnOn 输入大于侧输入就亮 相等时只有比较模式亮 对应原版覆写
        protected override bool ShouldTurnOn(ServerLevel level, BlockPos pos, BlockState state)
        {
            var input = GetInputSignal(level, pos, state);
            if (input == 0) return false;
            var sideInput = GetAlternateSignal(level, pos, state);
            if (input > sideInput) return true;
            return input == sideInput
                && state.GetValue(BlockStateProperties.ComparatorModeProperty) == ComparatorMode.compare;
        }

        //GetInputSignal 在前方信号基础上叠模拟量 对应原版覆写
        //前方方块有模拟输出就直接用它的 没有则看它后面那格 原版这一层还查物品展示框 本作没有展示框实体
        protected override int GetInputSignal(ServerLevel level, BlockPos pos, BlockState state)
        {
            var result = base.GetInputSignal(level, pos, state);
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var targetPos = pos.Offset(direction);
            if (level.GetBlockState(targetPos) is not { } target) return result;
            if (target.Owner is IBlockSignalBehaviour { HasAnalogOutputSignal: true } analog)
                return analog.GetAnalogOutputSignal(level, targetPos, target, direction.Opposite);
            if (result >= 15) return result;
            if (target.Owner is not BlockBehaviour conductor
                || !conductor.IsRedstoneConductor(level, targetPos, target))
                return result;
            //导体后方那一格给不给模拟量 比较器靠它读箱子那类容器
            var behindPos = targetPos.Offset(direction);
            if (level.GetBlockState(behindPos) is not { } behind) return result;
            return behind.Owner is IBlockSignalBehaviour { HasAnalogOutputSignal: true } behindAnalog
                ? behindAnalog.GetAnalogOutputSignal(level, behindPos, behind, direction.Opposite)
                : result;
        }

        //CalculateOutputSignal 算出这次该输出的强度 对应原版同名方法
        private int CalculateOutputSignal(ServerLevel level, BlockPos pos, BlockState state)
        {
            var input = GetInputSignal(level, pos, state);
            if (input == 0) return 0;
            var sideInput = GetAlternateSignal(level, pos, state);
            if (sideInput > input) return 0;
            return state.GetValue(BlockStateProperties.ComparatorModeProperty) == ComparatorMode.subtract
                ? input - sideInput
                : input;
        }

        //CheckTickOnNeighbor 输出值或通电态对不上才排刻 对应原版覆写
        //优先级与中继器不同 前方是反向二极管才抬高 其余一律普通
        protected override void CheckTickOnNeighbor(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (level.WillTickThisTick(pos, this)) return;
            var outputValue = CalculateOutputSignal(level, pos, state);
            var oldValue = level.GetBlockEntity<ComparatorBlockEntity>(pos)?.OutputSignal ?? 0;
            if (outputValue == oldValue
                && state.GetValue(BlockStateProperties.Powered) == ShouldTurnOn(level, pos, state))
                return;
            var priority = ShouldPrioritize(level, pos, state) ? TickPriority.High : TickPriority.Normal;
            level.ScheduleTick(pos, this, 2, priority);
        }

        //RefreshOutputState 写回输出值并按需翻转通电态 对应原版同名方法
        //比较模式下无论输出变没变都要重通知一次 侧输入变了会比较结果但输出值不变
        private void RefreshOutputState(ServerLevel level, BlockPos pos, BlockState state)
        {
            var outputValue = CalculateOutputSignal(level, pos, state);
            var entity = level.GetBlockEntity<ComparatorBlockEntity>(pos);
            var oldValue = entity?.OutputSignal ?? 0;
            entity?.SetOutputSignal(outputValue);
            if (oldValue == outputValue
                && state.GetValue(BlockStateProperties.ComparatorModeProperty) != ComparatorMode.compare)
                return;
            var sourceOn = ShouldTurnOn(level, pos, state);
            var isOn = state.GetValue(BlockStateProperties.Powered);
            if (isOn && !sourceOn)
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, false), BlockUpdateFlags.Clients);
            else if (!isOn && sourceOn)
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, true), BlockUpdateFlags.Clients);
            UpdateNeighborsInFront(level, pos, state);
        }

        //Tick 比较器到点直接重算输出 没有锁定一说 对应原版覆写
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
            => RefreshOutputState(level, pos, state);

        //UseOn 右键切换比较/减去 对应原版 useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            var newState = state.Cycle(BlockStateProperties.ComparatorModeProperty);
            //原版在这里播比较器点击音效 方块行为拿不到音效出口 与拉杆按钮一并留到音效出口开出来
            level.SetBlock(pos, newState, BlockUpdateFlags.Clients);
            if (ReferenceEquals(level.GetBlockState(pos)?.Owner, this))
                RefreshOutputState(level, pos, newState);
            return true;
        }
    }

    //RedstoneLampBlock 红石灯 对应原版 RedstoneLampBlock
    //通电立刻点亮 断电要等四刻才灭 点亮时自身发满级光
    public sealed class RedstoneLampBlock : BlockBehaviour
    {
        //LitDelay 熄灭的延迟刻数 对应原版 4
        private const int LitDelay = 4;

        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_lamp");

        //原版红石灯硬度 0.3
        public override float DestroySpeed => 0.3f;

        //GetLightEmission 点亮时发 15 级光 对应原版 lightLevel
        public override int GetLightEmission(BlockState state)
            => state.GetValue(BlockStateProperties.Lit) ? 15 : 0;

        //GetStateForPlacement 放下时就按周围信号决定亮不亮 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState.SetValue(BlockStateProperties.Lit, level.HasNeighborSignal(pos));

        //NeighborChanged 信号与点亮态不一致才动手 对应原版 neighborChanged
        //点亮是立即的 熄灭要排四刻 这是原版的节奏 缺了它灯会一通电就闪
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var lit = state.GetValue(BlockStateProperties.Lit);
            if (lit == level.HasNeighborSignal(pos)) return;
            if (lit) level.ScheduleTick(pos, this, LitDelay);
            else level.SetBlock(pos, state.Cycle(BlockStateProperties.Lit), BlockUpdateFlags.Clients);
        }

        //Tick 延迟到点复查 仍然没信号才熄灭 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Lit) && !level.HasNeighborSignal(pos))
                level.SetBlock(pos, state.Cycle(BlockStateProperties.Lit), BlockUpdateFlags.Clients);
        }
    }

    public static readonly RedstoneBlock REDSTONE_BLOCK = new();
    public static readonly RepeaterBlock REPEATER = new();
    public static readonly ComparatorBlock COMPARATOR = new();
    public static readonly LeverBlock LEVER = new();
    public static readonly RedstoneTorchBlock REDSTONE_TORCH = new();
    public static readonly RedstoneWallTorchBlock REDSTONE_WALL_TORCH = new();
    public static readonly RedstoneLampBlock REDSTONE_LAMP = new();

    //TargetBlock 标靶 对应原版 TargetBlock
    //被投射物命中按命中点算输出强度 越靠命中面中心越强 最弱 1 最强 15
    //箭类保持 20 刻 其他投射物 8 刻 保持期内再命中不覆盖强度
    public sealed class TargetBlock : BlockBehaviour
    {
        //ActivationTicksArrows 箭类命中后的保持刻数 对应原版 ACTIVATION_TICKS_ARROWS
        private const int ActivationTicksArrows = 20;

        //ActivationTicksOther 其他投射物命中后的保持刻数 对应原版 ACTIVATION_TICKS_OTHER
        private const int ActivationTicksOther = 8;

        public override Identifier Id => Identifier.WithDefaultNamespace("target");

        public override bool IsSignalSource => true;

        //OwnSignal 输出当前强度 对应原版 ownSignal
        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        //OnProjectileHit 命中时写入强度并排保持刻 对应原版 onProjectileHit
        public override void OnProjectileHit(ServerLevel level, BlockState state, BlockHitResult hit,
            Projectile projectile)
        {
            var strength = GetRedstoneStrength(hit);
            var duration = projectile is AbstractArrow ? ActivationTicksArrows : ActivationTicksOther;
            //还排在保持期说明强度已经在走 不再改写 对应原版 hasScheduledTick 判断
            if (level.HasScheduledTick(hit.BlockPos, this)) return;
            SetOutputPower(level, state, hit.BlockPos, strength, duration);
        }

        //Tick 保持期到点归零 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Power) != 0)
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, 0), BlockUpdateFlags.All);
        }

        //OnPlace 换上的标靶残留着强度又没排刻就清掉 对应原版 onPlace
        //写回带 KnownShape 免得清理这一步又触发形状更新
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            if (state.GetValue(BlockStateProperties.Power) <= 0 || level.HasScheduledTick(pos, this)) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, 0),
                BlockUpdateFlags.KnownShape | BlockUpdateFlags.Clients);
        }

        //SetOutputPower 写强度再排保持刻 对应原版 setOutputPower
        private void SetOutputPower(ServerLevel level, BlockState state, BlockPos pos, int strength, int duration)
        {
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, strength), BlockUpdateFlags.All);
            level.ScheduleTick(pos, this, duration);
        }

        //GetRedstoneStrength 偏移量取命中面之外两轴里大的那个 再线性映射到 1..15
        //对应原版 getRedstoneStrength 命中点在格内小数为 0.5 时偏移为 0 强度满
        private static int GetRedstoneStrength(BlockHitResult hit)
        {
            var location = hit.Location;
            var distX = Math.Abs(Mth.Frac(location.X) - 0.5);
            var distY = Math.Abs(Mth.Frac(location.Y) - 0.5);
            var distZ = Math.Abs(Mth.Frac(location.Z) - 0.5);
            var axis = hit.Direction.AxisValue;
            var distance = axis == Direction.Axis.Y
                ? Math.Max(distX, distZ)
                : axis == Direction.Axis.Z
                    ? Math.Max(distX, distY)
                    : Math.Max(distY, distZ);
            return Math.Max(1, Mth.Ceil(15.0 * Mth.Clamp((0.5 - distance) / 0.5, 0.0, 1.0)));
        }
    }

    public static readonly TargetBlock TARGET = new();

    //按钮 石制 20 刻 木制与菌类 30 刻 对应原版注册处的 ticksToStayPressed
    public static readonly ButtonBlock STONE_BUTTON = new("stone_button", 20);
    public static readonly ButtonBlock OAK_BUTTON = new("oak_button", 30);
    public static readonly ButtonBlock SPRUCE_BUTTON = new("spruce_button", 30);
    public static readonly ButtonBlock BIRCH_BUTTON = new("birch_button", 30);
    public static readonly ButtonBlock JUNGLE_BUTTON = new("jungle_button", 30);
    public static readonly ButtonBlock ACACIA_BUTTON = new("acacia_button", 30);
    public static readonly ButtonBlock CHERRY_BUTTON = new("cherry_button", 30);
    public static readonly ButtonBlock DARK_OAK_BUTTON = new("dark_oak_button", 30);
    public static readonly ButtonBlock PALE_OAK_BUTTON = new("pale_oak_button", 30);
    public static readonly ButtonBlock MANGROVE_BUTTON = new("mangrove_button", 30);
    public static readonly ButtonBlock BAMBOO_BUTTON = new("bamboo_button", 30);
    public static readonly ButtonBlock CRIMSON_BUTTON = new("crimson_button", 30);
    public static readonly ButtonBlock WARPED_BUTTON = new("warped_button", 30);
    public static readonly ButtonBlock POLISHED_BLACKSTONE_BUTTON = new("polished_blackstone_button", 20);

    //简单压力板 踩上去就给满
    public static readonly PressurePlateBlock STONE_PRESSURE_PLATE = new("stone_pressure_plate");
    public static readonly PressurePlateBlock OAK_PRESSURE_PLATE = new("oak_pressure_plate");
    public static readonly PressurePlateBlock SPRUCE_PRESSURE_PLATE = new("spruce_pressure_plate");
    public static readonly PressurePlateBlock BIRCH_PRESSURE_PLATE = new("birch_pressure_plate");
    public static readonly PressurePlateBlock JUNGLE_PRESSURE_PLATE = new("jungle_pressure_plate");
    public static readonly PressurePlateBlock ACACIA_PRESSURE_PLATE = new("acacia_pressure_plate");
    public static readonly PressurePlateBlock CHERRY_PRESSURE_PLATE = new("cherry_pressure_plate");
    public static readonly PressurePlateBlock DARK_OAK_PRESSURE_PLATE = new("dark_oak_pressure_plate");
    public static readonly PressurePlateBlock PALE_OAK_PRESSURE_PLATE = new("pale_oak_pressure_plate");
    public static readonly PressurePlateBlock MANGROVE_PRESSURE_PLATE = new("mangrove_pressure_plate");
    public static readonly PressurePlateBlock BAMBOO_PRESSURE_PLATE = new("bamboo_pressure_plate");
    public static readonly PressurePlateBlock CRIMSON_PRESSURE_PLATE = new("crimson_pressure_plate");
    public static readonly PressurePlateBlock WARPED_PRESSURE_PLATE = new("warped_pressure_plate");
    public static readonly PressurePlateBlock POLISHED_BLACKSTONE_PRESSURE_PLATE =
        new("polished_blackstone_pressure_plate");

    //测重压力板 轻质金制最大承重 15 重质铁制 150 对应原版注册
    public static readonly WeightedPressurePlateBlock LIGHT_WEIGHTED_PRESSURE_PLATE =
        new("light_weighted_pressure_plate", 15);
    public static readonly WeightedPressurePlateBlock HEAVY_WEIGHTED_PRESSURE_PLATE =
        new("heavy_weighted_pressure_plate", 150);

    //RegisterRedstone 红石元件登记进真实方块表 数量多按字段遍历登记 键取注册名
    private static void RegisterRedstone(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks =
        {
            REDSTONE_BLOCK, REDSTONE_WIRE, LEVER, REDSTONE_TORCH, REDSTONE_WALL_TORCH, REPEATER, COMPARATOR,
            OBSERVER, REDSTONE_LAMP, TARGET,
            STONE_BUTTON, OAK_BUTTON, SPRUCE_BUTTON, BIRCH_BUTTON, JUNGLE_BUTTON, ACACIA_BUTTON,
            CHERRY_BUTTON, DARK_OAK_BUTTON, PALE_OAK_BUTTON, MANGROVE_BUTTON, BAMBOO_BUTTON,
            CRIMSON_BUTTON, WARPED_BUTTON, POLISHED_BLACKSTONE_BUTTON,
            STONE_PRESSURE_PLATE, OAK_PRESSURE_PLATE, SPRUCE_PRESSURE_PLATE, BIRCH_PRESSURE_PLATE,
            JUNGLE_PRESSURE_PLATE, ACACIA_PRESSURE_PLATE, CHERRY_PRESSURE_PLATE, DARK_OAK_PRESSURE_PLATE,
            PALE_OAK_PRESSURE_PLATE, MANGROVE_PRESSURE_PLATE, BAMBOO_PRESSURE_PLATE, CRIMSON_PRESSURE_PLATE,
            WARPED_PRESSURE_PLATE, POLISHED_BLACKSTONE_PRESSURE_PLATE,
            LIGHT_WEIGHTED_PRESSURE_PLATE, HEAVY_WEIGHTED_PRESSURE_PLATE,
        };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }
}
