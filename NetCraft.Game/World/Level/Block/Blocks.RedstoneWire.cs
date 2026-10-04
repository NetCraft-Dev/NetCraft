using NetCraft.Game.Server;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Redstone;
using NetCraft.Storage.Updates;
using RedstoneSide = NetCraft.Registry.Enums.RedstoneSide;

namespace NetCraft.Game.World.Level.Block;

//Blocks 红石线部分 与 Blocks.cs 同一个类分开文件免得主文件太长
//红石线是整个红石系统的骨架 连接模型决定外观与对接 功率传播决定信号怎么衰减
public static partial class Blocks
{
    //RedstoneWireBlock 红石线 对应原版 RedStoneWireBlock
    //四向连接状态各三值 加上 0-15 功率 一共 1296 个状态
    //信号只沿连上的方向传 每根线把收到的最大信号减一后作为自己的功率
    public sealed class RedstoneWireBlock : BlockBehaviour
    {
        //_shouldSignal 读邻居信号期间要临时关掉自己的信号源标记
        //不关的话 getBestNeighborSignal 会把邻接红石线的信号也算进来 形成自反馈
        private bool _shouldSignal = true;

        //_crossState 十字态四向都连上 点击切换与形状重算的起点 对应原版 crossState
        private BlockState? _crossState;

        //形状按四面连接方式组合 3^4 种静态一次算完 对应原版 makeShapes 里的 getShapeForEachState
        //中心点 + 四向水平薄板 上爬方向再叠一块竖板
        //缺了它形状落到默认整格 该格天光被当成实心挡住 红石线那格会比原版暗
        private static readonly Dictionary<(RedstoneSide, RedstoneSide, RedstoneSide, RedstoneSide), VoxelShape>
            ShapeTable = BuildShapeTable();

        private static Dictionary<(RedstoneSide, RedstoneSide, RedstoneSide, RedstoneSide), VoxelShape>
            BuildShapeTable()
        {
            //中心圆点 10 像素宽 1 像素高
            var dot = NetCraft.Registry.Block.Column(10.0, 0.0, 1.0);
            //水平延伸板 从中心伸到该方向的半格处
            var floor = Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(10.0, 0.0, 1.0, 0.0, 8.0));
            //上爬竖板 满高且贴在该方向的内侧
            var up = Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(10.0, 16.0, 0.0, 1.0));
            var sides = new[] { RedstoneSide.none, RedstoneSide.side, RedstoneSide.up };
            var table = new Dictionary<(RedstoneSide, RedstoneSide, RedstoneSide, RedstoneSide), VoxelShape>();
            foreach (var north in sides)
            foreach (var east in sides)
            foreach (var south in sides)
            foreach (var west in sides)
            {
                var shape = Extend(dot, north, Direction.North, floor, up);
                shape = Extend(shape, east, Direction.East, floor, up);
                shape = Extend(shape, south, Direction.South, floor, up);
                shape = Extend(shape, west, Direction.West, floor, up);
                table[(north, east, south, west)] = shape;
            }
            return table;
        }

        //Extend 把某一向的连接形状并进整体形状 上爬态叠竖板 对应原版那个 switch
        private static VoxelShape Extend(VoxelShape shape, RedstoneSide side, Direction direction,
            Dictionary<Direction, VoxelShape> floor, Dictionary<Direction, VoxelShape> up)
            => side switch
            {
                RedstoneSide.up => Shapes.Or(Shapes.Or(shape, floor[direction]), up[direction]),
                RedstoneSide.side => Shapes.Or(shape, floor[direction]),
                _ => shape,
            };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => ShapeTable[(
                state.GetValue(BlockStateProperties.NorthRedstone),
                state.GetValue(BlockStateProperties.EastRedstone),
                state.GetValue(BlockStateProperties.SouthRedstone),
                state.GetValue(BlockStateProperties.WestRedstone))];

        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_wire");

        //原版红石线 instabreak 硬度 0 空手秒破
        public override float DestroySpeed => 0f;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["east"] = BlockStateProperties.EastRedstone,
            ["north"] = BlockStateProperties.NorthRedstone,
            ["power"] = BlockStateProperties.Power,
            ["south"] = BlockStateProperties.SouthRedstone,
            ["west"] = BlockStateProperties.WestRedstone,
        };

        //红石线不是导体也不是完整立方体 原版靠 noCollision 让默认谓词落空
        //不覆写的话 NC 的整格实心近似会把红石线判成导体 信号就不衰减了
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsFaceSturdy(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => false;

        //IsSignalSource 读邻居期间关掉 对应原版 shouldSignal 字段
        public override bool IsSignalSource => _shouldSignal;

        //原版默认态四向 NONE 功率 0
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.NorthRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.EastRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.SouthRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.WestRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.Power, 0);

        //CrossState 十字态四向都算连上 对应原版 crossState
        private BlockState CrossState => _crossState ??= DefaultBlockState
            .SetValue(BlockStateProperties.NorthRedstone, RedstoneSide.side)
            .SetValue(BlockStateProperties.EastRedstone, RedstoneSide.side)
            .SetValue(BlockStateProperties.SouthRedstone, RedstoneSide.side)
            .SetValue(BlockStateProperties.WestRedstone, RedstoneSide.side);

        //CanSurvive 下方那格朝上的面够坚固或本身是漏斗 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            return level.GetBlockState(below) is { } support && CanSurviveOn(level, below, support);
        }

        //CanSurviveOn 能否当红石线的支撑 对应原版 canSurviveOn
        //原版活板门单独放行 本作活板门还是占位块 整格实心近似已经覆盖 不再单列
        private static bool CanSurviveOn(ServerLevel level, BlockPos supportPos, BlockState supportState)
            => IsFaceSturdyAt(level, supportPos, supportState, Direction.Up)
                || supportState.Owner.Id == RedstoneIds.Hopper;

        //GetStateForPlacement 放下时按周围算一遍连接 对应原版同名方法
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing) => GetConnectionState(level, CrossState, pos);

        //GetConnectionState 先补齐缺的连接再按十字/点补 SIDE 对应原版同名方法
        //点是四向都没连 十字是四向都连 这两种形态点击时会互相切换所以单独判
        private static BlockState GetConnectionState(ServerLevel level, BlockState state, BlockPos pos)
        {
            var wasDot = IsDot(state);
            var computed = GetMissingConnections(level, DefaultWireStateWithPower(state), pos);
            if (wasDot && IsDot(computed)) return computed;
            var northSouthEmpty = !computed.GetValue(BlockStateProperties.NorthRedstone).IsConnected()
                && !computed.GetValue(BlockStateProperties.SouthRedstone).IsConnected();
            var eastWestEmpty = !computed.GetValue(BlockStateProperties.EastRedstone).IsConnected()
                && !computed.GetValue(BlockStateProperties.WestRedstone).IsConnected();
            //南北都空就补上东南西 东西都空就补上南北 补出来的是 SIDE 不是 UP
            if (!computed.GetValue(BlockStateProperties.WestRedstone).IsConnected() && northSouthEmpty)
                computed = computed.SetValue(BlockStateProperties.WestRedstone, RedstoneSide.side);
            if (!computed.GetValue(BlockStateProperties.EastRedstone).IsConnected() && northSouthEmpty)
                computed = computed.SetValue(BlockStateProperties.EastRedstone, RedstoneSide.side);
            if (!computed.GetValue(BlockStateProperties.NorthRedstone).IsConnected() && eastWestEmpty)
                computed = computed.SetValue(BlockStateProperties.NorthRedstone, RedstoneSide.side);
            if (!computed.GetValue(BlockStateProperties.SouthRedstone).IsConnected() && eastWestEmpty)
                computed = computed.SetValue(BlockStateProperties.SouthRedstone, RedstoneSide.side);
            return computed;
        }

        //DefaultWireStateWithPower 取默认态但保留传入状态的功率 对应原版那两处 setValue(POWER, ...)
        private static BlockState DefaultWireStateWithPower(BlockState state)
            => REDSTONE_WIRE.DefaultBlockState.SetValue(BlockStateProperties.Power,
                state.GetValue(BlockStateProperties.Power));

        //GetMissingConnections 给四向里还没连上的重算连接 对应原版同名方法
        //canConnectUp 决定这一轮能不能把连接抬到 UP 上方是导体的话红石线爬不上去
        private static BlockState GetMissingConnections(ServerLevel level, BlockState state, BlockPos pos)
        {
            var canConnectUp = !IsRedstoneConductor(level, pos.Offset(Direction.Up));
            foreach (var direction in HorizontalDirections)
            {
                var property = SideProperty(direction);
                if (state.GetValue(property).IsConnected()) continue;
                state = state.SetValue(property, GetConnectingSide(level, pos, direction, canConnectUp));
            }
            return state;
        }

        //GetConnectingSide 算某一侧该连成什么 对应原版两个重载
        private static RedstoneSide GetConnectingSide(ServerLevel level, BlockPos pos, Direction direction)
            => GetConnectingSide(level, pos, direction, !IsRedstoneConductor(level, pos.Offset(Direction.Up)));

        private static RedstoneSide GetConnectingSide(ServerLevel level, BlockPos pos, Direction direction,
            bool canConnectUp)
        {
            var neighbourPos = pos.Offset(direction);
            if (level.GetBlockState(neighbourPos) is not { } neighbour) return RedstoneSide.none;
            if (canConnectUp)
            {
                //邻居上方能架线且上方那格也对接得上 就把连接抬到 UP
                //上方站不稳时退成 SIDE 对应原版那个 isPlaceableAbove 分支
                var canPlaceAbove = CanSurviveOn(level, neighbourPos, neighbour);
                if (canPlaceAbove
                    && ShouldConnectTo(level.GetBlockState(neighbourPos.Offset(Direction.Up))))
                {
                    return IsFaceSturdyAt(level, neighbourPos, neighbour, direction.Opposite)
                        ? RedstoneSide.up
                        : RedstoneSide.side;
                }
            }
            if (ShouldConnectTo(neighbour, direction)
                || (!IsRedstoneConductor(level, neighbourPos)
                    && ShouldConnectTo(level.GetBlockState(neighbourPos.Offset(Direction.Down)))))
                return RedstoneSide.side;
            return RedstoneSide.none;
        }

        //ShouldConnectTo 该方块能不能跟红石线对接 对应原版两个重载
        //红石线恒连 中继器两个口都连 观察者只连它输出那一侧 其余信号源也算连
        private static bool ShouldConnectTo(BlockState? state, Direction? direction = null)
        {
            if (state is not { } block) return false;
            if (block.Owner.Id == RedstoneIds.Wire) return true;
            if (block.Owner.Id != RedstoneIds.Repeater)
            {
                //观察者只朝 FACING 那一侧对接 观察者 P2-6 才落地 之前它是占位块没有这个属性
                if (block.Owner.Id == RedstoneIds.Observer)
                    return direction is { } side
                        && block.HasProperty(BlockStateProperties.FacingProperty)
                        && block.GetValue(BlockStateProperties.FacingProperty).ToPrimitive() == side;
                return direction is not null && block.Owner is IBlockSignalBehaviour { IsSignalSource: true };
            }
            var repeaterDirection = block.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            return repeaterDirection == direction || repeaterDirection.Opposite == direction;
        }

        //UpdateShape 上方变化重算整个连接 侧面变化按需重算或整体重算 对应原版同名方法
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Down)
                return CanSurviveOn(level, neighbourPos, neighbourState) ? state : AIR.DefaultBlockState;
            if (directionToNeighbour == Direction.Up)
                return GetConnectionState(level, state, pos);
            var property = SideProperty(directionToNeighbour);
            var sideConnection = GetConnectingSide(level, pos, directionToNeighbour);
            //连没连上没变且本来就是十字 只改这一侧的值就够了
            if (sideConnection.IsConnected() == state.GetValue(property).IsConnected() && !IsCross(state))
                return state.SetValue(property, sideConnection);
            var baseState = CrossState.SetValue(BlockStateProperties.Power, state.GetValue(BlockStateProperties.Power));
            return GetConnectionState(level, baseState.SetValue(property, sideConnection), pos);
        }

        //UpdateIndirectNeighbourShapes 角落联动 对应原版同名方法
        //邻格不是红石线时 它上下两格的斜角红石线要跟着重算连接 这就是红石线的爬坡与下坡
        public override void UpdateIndirectNeighbourShapes(ServerLevel level, BlockPos pos, BlockState state,
            int updateFlags, int updateLimit)
        {
            foreach (var direction in HorizontalDirections)
            {
                if (state.GetValue(SideProperty(direction)) == RedstoneSide.none) continue;
                var neighbourPos = pos.Offset(direction);
                if (level.GetBlockState(neighbourPos)?.Owner.Id == RedstoneIds.Wire) continue;
                ShapeUpdateCorner(level, neighbourPos.Offset(Direction.Down), direction, updateFlags, updateLimit);
                ShapeUpdateCorner(level, neighbourPos.Offset(Direction.Up), direction, updateFlags, updateLimit);
            }
        }

        //ShapeUpdateCorner 斜角那格是红石线就给它发一次形状更新 对应原版那两段重复代码
        private static void ShapeUpdateCorner(ServerLevel level, BlockPos cornerPos, Direction direction,
            int updateFlags, int updateLimit)
        {
            if (level.GetBlockState(cornerPos)?.Owner.Id != RedstoneIds.Wire) return;
            var sourcePos = cornerPos.Offset(direction.Opposite);
            level.NeighborShapeChanged(direction.Opposite, cornerPos, sourcePos,
                level.GetBlockState(sourcePos) ?? AIR.DefaultBlockState, updateFlags, updateLimit);
        }

        //GetSignal 只有连上的方向才给信号 朝上恒给 对应原版同名方法
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        {
            if (!_shouldSignal || direction == Direction.Down) return 0;
            var power = OwnSignal(level, pos, state);
            if (power == 0) return 0;
            if (direction == Direction.Up) return power;
            //查询者在 direction 反向侧 要看的是那一侧连没连上
            var property = SideProperty(direction.Opposite);
            return GetConnectionState(level, state, pos).GetValue(property).IsConnected() ? power : 0;
        }

        //GetDirectSignal 与自身信号一致 对应原版同名方法
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => _shouldSignal ? GetSignal(level, pos, state, direction) : 0;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        //GetBlockSignal 邻居给的最强信号 读之前先关掉自己的信号源标记 对应原版同名方法
        //不关的话邻接红石线会把自己的功率当成新信号源回灌 红石线就永远衰减不下去
        public int GetBlockSignal(ServerLevel level, BlockPos pos)
        {
            _shouldSignal = false;
            var signal = level.GetBestNeighborSignal(pos);
            _shouldSignal = true;
            return signal;
        }

        //UpdatePowerStrength 算目标功率并写回 对应原版 DefaultRedstoneWireEvaluator
        //写回只用 Clients 位 改动之后本体与六向邻居各自的邻接都要重算
        private void UpdatePowerStrength(ServerLevel level, BlockPos pos, BlockState state)
        {
            var targetStrength = CalculateTargetStrength(level, pos);
            if (state.GetValue(BlockStateProperties.Power) == targetStrength) return;
            if (level.GetBlockState(pos) is { } current && current == state)
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, targetStrength),
                    BlockUpdateFlags.Clients);
            level.UpdateNeighborsAt(pos, this);
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //CalculateTargetStrength 邻居信号与相邻红石线来的信号取最大 对应原版同名方法
        private int CalculateTargetStrength(ServerLevel level, BlockPos pos)
        {
            var blockSignal = GetBlockSignal(level, pos);
            if (blockSignal == 15) return blockSignal;
            return Math.Max(blockSignal, GetIncomingWireSignal(level, pos));
        }

        //GetIncomingWireSignal 四向邻格及其上下斜角的红石线功率 减一后取最大 对应原版同名方法
        //减一就是红石线每走一格衰减一级 斜角那两格绕过了这一级衰减的判定由邻居是不是导体决定
        private static int GetIncomingWireSignal(ServerLevel level, BlockPos pos)
        {
            var abovePos = pos.Offset(Direction.Up);
            var wireSignal = 0;
            foreach (var direction in HorizontalDirections)
            {
                var neighbourPos = pos.Offset(direction);
                if (level.GetBlockState(neighbourPos) is not { } neighbour) continue;
                wireSignal = Math.Max(wireSignal, GetWireSignal(neighbour));
                if (IsRedstoneConductor(level, neighbourPos) && !IsRedstoneConductor(level, abovePos))
                {
                    var aboveNeighbourPos = neighbourPos.Offset(Direction.Up);
                    wireSignal = Math.Max(wireSignal,
                        GetWireSignal(level.GetBlockState(aboveNeighbourPos)));
                }
                else if (!IsRedstoneConductor(level, neighbourPos))
                {
                    var belowNeighbourPos = neighbourPos.Offset(Direction.Down);
                    wireSignal = Math.Max(wireSignal,
                        GetWireSignal(level.GetBlockState(belowNeighbourPos)));
                }
            }
            return Math.Max(0, wireSignal - 1);
        }

        //GetWireSignal 那格是红石线就取它的功率 对应原版同名方法
        private static int GetWireSignal(BlockState? state)
            => state is { } wire && wire.Owner.Id == RedstoneIds.Wire
                ? wire.GetValue(BlockStateProperties.Power)
                : 0;

        //NeighborChanged 邻接变化重算功率 站不住就掉 对应原版同名方法
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            if (CanSurvive(level, pos, state))
            {
                UpdatePowerStrength(level, pos, state);
                return;
            }
            level.BlockUpdateSink?.DestroyBlock(pos, true, BlockUpdateFlags.UpdateLimitDefault);
        }

        //OnPlace 放下后先算一次功率再通知上下与周围的红石线 对应原版同名方法
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdatePowerStrength(level, pos, state);
            level.UpdateNeighborsAt(pos.Offset(Direction.Up), this);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), this);
            UpdateNeighborsOfNeighboringWires(level, pos);
        }

        //AffectNeighborsAfterRemoval 拆掉后六向都要重算 对应原版同名方法
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
            UpdatePowerStrength(level, pos, state);
            UpdateNeighborsOfNeighboringWires(level, pos);
        }

        //UpdateNeighborsOfNeighboringWires 周围红石线的角落也要跟着动 对应原版同名方法
        private void UpdateNeighborsOfNeighboringWires(ServerLevel level, BlockPos pos)
        {
            foreach (var direction in HorizontalDirections)
                CheckCornerChangeAt(level, pos.Offset(direction));
            foreach (var direction in HorizontalDirections)
            {
                var target = pos.Offset(direction);
                CheckCornerChangeAt(level, IsRedstoneConductor(level, target)
                    ? target.Offset(Direction.Up)
                    : target.Offset(Direction.Down));
            }
        }

        //CheckCornerChangeAt 那格是红石线就让本体与六向邻居都重算 对应原版同名方法
        private void CheckCornerChangeAt(ServerLevel level, BlockPos pos)
        {
            if (level.GetBlockState(pos)?.Owner.Id != RedstoneIds.Wire) return;
            level.UpdateNeighborsAt(pos, this);
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //UseOn 右键在十字与点之间切换 对应原版 useWithoutItem
        //原版只在本来就是十字或点的时候才动手 普通拐角形状点了没反应
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            if (!IsCross(state) && !IsDot(state)) return false;
            var baseState = IsCross(state) ? DefaultBlockState : CrossState;
            var newState = GetConnectionState(level,
                baseState.SetValue(BlockStateProperties.Power, state.GetValue(BlockStateProperties.Power)), pos);
            if (newState == state) return false;
            level.SetBlock(pos, newState, BlockUpdateFlags.All);
            UpdatesOnShapeChange(level, pos, state, newState);
            return true;
        }

        //UpdatesOnShapeChange 连接状态变了的那些方向要通知到支撑方块 对应原版同名方法
        private static void UpdatesOnShapeChange(ServerLevel level, BlockPos pos, BlockState oldState,
            BlockState newState)
        {
            foreach (var direction in HorizontalDirections)
            {
                var property = SideProperty(direction);
                var neighbourPos = pos.Offset(direction);
                if (oldState.GetValue(property).IsConnected() == newState.GetValue(property).IsConnected())
                    continue;
                if (IsRedstoneConductor(level, neighbourPos))
                    level.UpdateNeighborsAtExceptFromFacing(neighbourPos, newState.Owner, direction.Opposite);
            }
        }

        //IsCross 四向都连上 对应原版同名方法
        private static bool IsCross(BlockState state)
            => state.GetValue(BlockStateProperties.NorthRedstone).IsConnected()
                && state.GetValue(BlockStateProperties.SouthRedstone).IsConnected()
                && state.GetValue(BlockStateProperties.EastRedstone).IsConnected()
                && state.GetValue(BlockStateProperties.WestRedstone).IsConnected();

        //IsDot 四向都没连 对应原版同名方法
        private static bool IsDot(BlockState state)
            => !state.GetValue(BlockStateProperties.NorthRedstone).IsConnected()
                && !state.GetValue(BlockStateProperties.SouthRedstone).IsConnected()
                && !state.GetValue(BlockStateProperties.EastRedstone).IsConnected()
                && !state.GetValue(BlockStateProperties.WestRedstone).IsConnected();

        //HorizontalDirections 水平四向 顺序照原版 Direction.Plane.HORIZONTAL
        //红石的更新顺序敏感 不能按轴另排
        private static readonly Direction[] HorizontalDirections =
        {
            Direction.North, Direction.South, Direction.West, Direction.East,
        };

        //SideProperty 水平方向取对应的连接属性 对应原版 PROPERTY_BY_DIRECTION
        //只对水平方向有定义 调用点必须先过滤 与原版 EnumMap 落到 null 的行为一致
        private static EnumProperty<RedstoneSide> SideProperty(Direction direction) => direction.Id3D switch
        {
            Direction.NorthId => BlockStateProperties.NorthRedstone,
            Direction.SouthId => BlockStateProperties.SouthRedstone,
            Direction.WestId => BlockStateProperties.WestRedstone,
            _ => BlockStateProperties.EastRedstone,
        };

        //IsRedstoneConductor 那格是不是红石导体 对应原版 BlockState.isRedstoneConductor
        private static bool IsRedstoneConductor(ServerLevel level, BlockPos pos)
            => level.GetBlockState(pos) is { } state
                && state.Owner is IBlockSignalBehaviour behaviour
                && behaviour.IsRedstoneConductor(level, pos, state);

        //IsFaceSturdyAt 那格朝某面是否够坚固 对应原版 BlockState.isFaceSturdy
        //面坚固不在信号契约里 只有方块行为自己知道 所以按 BlockBehaviour 取
        //名字避开本类覆写的 IsFaceSturdy
        private static bool IsFaceSturdyAt(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.Owner is BlockBehaviour behaviour && behaviour.IsFaceSturdy(level, pos, state, direction);
    }

    public static readonly RedstoneWireBlock REDSTONE_WIRE = new();
}
