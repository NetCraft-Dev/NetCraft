using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Game.World.Phys.Collision;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//铁轨四种 对应原版 RailBlock / PoweredRailBlock / DetectorRailBlock / ActivatorRailBlock
//形状推导走 RailState 那一套: 把当前形状拆成两个连接点 再看四邻有没有铁轨来重定形状
//动力铁轨的充能沿轨道传播最多八格 探测与激活铁轨的矿车动作依赖矿车实体 本作未接入
public static partial class Blocks
{
    public static readonly RailBlock RAIL = new();
    public static readonly PoweredRailBlock POWERED_RAIL = new();
    public static readonly DetectorRailBlock DETECTOR_RAIL = new();
    public static readonly ActivatorRailBlock ACTIVATOR_RAIL = new();

    //RegisterRails 铁轨四种登记进真实方块表
    private static void RegisterRails(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { RAIL, POWERED_RAIL, DETECTOR_RAIL, ACTIVATOR_RAIL };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //BaseRail 铁轨基类 对应原版 BaseRailBlock
    //平放两像素高 上坡八像素 支撑没了或者上坡那一侧是空就掉
    public abstract class BaseRail : BlockBehaviour
    {
        private static readonly VoxelShape FlatShape = NetCraft.Registry.Block.Column(16.0, 0.0, 2.0);
        private static readonly VoxelShape SlopeShape = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);

        //IsStraight 是不是直道铁轨 只有普通铁轨能弯
        protected abstract bool IsStraight { get; }

        //RailShapeProperty 本方块用的形状属性实例 直道六种弯道十种
        protected abstract EnumProperty<RailShape> RailShapeProperty { get; }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(RailShapeProperty).IsSlope() ? SlopeShape : FlatShape;

        //CanSurvive 下方要能顶住 对应原版 canSupportRigidBlock
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
            => CanSupportRigid(level, pos.Offset(Direction.Down));

        //OnPlace 换成铁轨那刻重算形状 对应原版 onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdateState(level, pos, state, movedByPiston);
        }

        //NeighborChanged 支撑没了就掉 否则重算形状 对应原版 neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var gone = ShouldBeRemoved(level, pos, state.GetValue(RailShapeProperty));
            if (gone)
            {
                level.BlockUpdateSink?.DestroyBlock(pos, true, BlockUpdateFlags.UpdateLimitDefault);
                return;
            }
            UpdateState(level, pos, state, movedByPiston);
        }

        //AffectNeighborsAfterRemoval 移走后通知上方与下方 对应原版 affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            if (state.GetValue(RailShapeProperty).IsSlope())
                level.UpdateNeighborsAt(pos.Offset(Direction.Up), state.Owner);
            if (!IsStraight) return;
            level.UpdateNeighborsAt(pos, state.Owner);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), state.Owner);
        }

        //GetStateForPlacement 东西向摆朝向就给东西 否则南北 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
        {
            var eastWest = horizontalFacing == Direction.East || horizontalFacing == Direction.West;
            return DefaultBlockState
                .SetValue(RailShapeProperty, eastWest ? RailShape.east_west : RailShape.north_south)
                .SetValue(BlockStateProperties.Waterlogged, false);
        }

        //UpdateState 先按连接重算形状 直道铁轨再走一遍自己的通电判定
        //原版是借 level.neighborChanged 再入一次这里直接调 免得在 SetBlock 流程里重排队列
        protected virtual void UpdateState(ServerLevel level, BlockPos pos, BlockState state, bool movedByPiston)
        {
            var shaped = UpdateDir(level, pos, state, true);
            if (IsStraight) UpdatePoweredState(level, pos, shaped);
        }

        //UpdatePoweredState 直道铁轨的通电判定 对应原版四参 updateState 由子类覆写
        protected virtual void UpdatePoweredState(ServerLevel level, BlockPos pos, BlockState state) { }

        //UpdateDir 按 RaiState 重算形状 对应原版 updateDir
        private BlockState UpdateDir(ServerLevel level, BlockPos pos, BlockState state, bool first)
        {
            var current = state.GetValue(RailShapeProperty);
            return new RailState(level, pos, state).Place(level.HasNeighborSignal(pos), first, current).State;
        }

        //ShouldBeRemoved 支撑没了或者上坡那一侧悬空 对应原版 shouldBeRemoved
        private static bool ShouldBeRemoved(ServerLevel level, BlockPos pos, RailShape shape)
        {
            if (!CanSupportRigid(level, pos.Offset(Direction.Down))) return true;
            return shape switch
            {
                RailShape.ascending_east => !CanSupportRigid(level, pos.Offset(Direction.East)),
                RailShape.ascending_west => !CanSupportRigid(level, pos.Offset(Direction.West)),
                RailShape.ascending_north => !CanSupportRigid(level, pos.Offset(Direction.North)),
                RailShape.ascending_south => !CanSupportRigid(level, pos.Offset(Direction.South)),
                _ => false,
            };
        }

        //CanSupportRigid 该格能不能托住铁轨 对应原版 canSupportRigidBlock
        //用空世界视图判形状即可 铁轨要的是 RIGID 那一档(外圈边框顶住)
        private static bool CanSupportRigid(ServerLevel level, BlockPos pos)
        {
            if (level.GetBlockState(pos) is not { } state) return false;
            return SupportType.Rigid.IsSupporting(state, EmptyBlockGetter.Instance, pos, Direction.Up);
        }

        //IsRail 该格就是铁轨 对应原版 isRail(level, pos) 只认这一格
        //上坡判定用它 早先误用三段查找会把下方铁轨也算进来 会把平轨判成上坡
        private static bool IsRail(ServerLevel level, BlockPos pos)
            => level.GetBlockState(pos) is { Owner: BaseRail };

        //RailState 铁轨连线推导 对应原版 RailState
        //形状决定两个连接点 连接点上有能接上的铁轨就保持 然后按四邻重新定形状
        private sealed class RailState
        {
            private readonly ServerLevel _level;
            private readonly BlockPos _pos;
            private readonly BaseRail _block;
            private readonly bool _isStraight;
            private readonly List<BlockPos> _connections = new();
            private BlockState _state;

            public RailState(ServerLevel level, BlockPos pos, BlockState state)
            {
                _level = level;
                _pos = pos;
                _state = state;
                _block = (BaseRail)state.Owner;
                _isStraight = _block.IsStraight;
                UpdateConnections(state.GetValue(_block.RailShapeProperty));
            }

            public BlockState State => _state;

            //UpdateConnections 按形状把两个连接点铺成相邻格 对应原版 updateConnections
            private void UpdateConnections(RailShape shape)
            {
                _connections.Clear();
                switch (shape)
                {
                    case RailShape.north_south:
                        _connections.Add(_pos.Offset(Direction.North));
                        _connections.Add(_pos.Offset(Direction.South));
                        break;
                    case RailShape.east_west:
                        _connections.Add(_pos.Offset(Direction.West));
                        _connections.Add(_pos.Offset(Direction.East));
                        break;
                    case RailShape.ascending_east:
                        _connections.Add(_pos.Offset(Direction.West));
                        _connections.Add(_pos.Offset(Direction.East).Offset(Direction.Up));
                        break;
                    case RailShape.ascending_west:
                        _connections.Add(_pos.Offset(Direction.West).Offset(Direction.Up));
                        _connections.Add(_pos.Offset(Direction.East));
                        break;
                    case RailShape.ascending_north:
                        _connections.Add(_pos.Offset(Direction.North).Offset(Direction.Up));
                        _connections.Add(_pos.Offset(Direction.South));
                        break;
                    case RailShape.ascending_south:
                        _connections.Add(_pos.Offset(Direction.North));
                        _connections.Add(_pos.Offset(Direction.South).Offset(Direction.Up));
                        break;
                    case RailShape.south_east:
                        _connections.Add(_pos.Offset(Direction.East));
                        _connections.Add(_pos.Offset(Direction.South));
                        break;
                    case RailShape.south_west:
                        _connections.Add(_pos.Offset(Direction.West));
                        _connections.Add(_pos.Offset(Direction.South));
                        break;
                    case RailShape.north_west:
                        _connections.Add(_pos.Offset(Direction.West));
                        _connections.Add(_pos.Offset(Direction.North));
                        break;
                    case RailShape.north_east:
                        _connections.Add(_pos.Offset(Direction.East));
                        _connections.Add(_pos.Offset(Direction.North));
                        break;
                }
            }

            //RemoveSoftConnections 连接点上没有能接上的铁轨就把它去掉 对应原版 removeSoftConnections
            private void RemoveSoftConnections()
            {
                for (var i = 0; i < _connections.Count; i++)
                {
                    var rail = GetRail(_connections[i]);
                    if (rail is null || !rail.ConnectsTo(this))
                    {
                        _connections.RemoveAt(i--);
                        continue;
                    }
                    _connections[i] = rail._pos;
                }
            }

            //GetRail 该位置连同上下两格找一根铁轨 对应原版 getRail
            private RailState? GetRail(BlockPos pos)
            {
                if (_level.GetBlockState(pos) is { Owner: BaseRail } here)
                    return new RailState(_level, pos, here);
                var above = pos.Offset(Direction.Up);
                if (_level.GetBlockState(above) is { Owner: BaseRail } up)
                    return new RailState(_level, above, up);
                var below = pos.Offset(Direction.Down);
                if (_level.GetBlockState(below) is { Owner: BaseRail } down)
                    return new RailState(_level, below, down);
                return null;
            }

            private bool ConnectsTo(RailState rail) => HasConnection(rail._pos);

            //HasConnection 连接点里有没有这一格 只比水平坐标 对应原版 hasConnection
            private bool HasConnection(BlockPos railPos)
            {
                foreach (var pos in _connections)
                    if (pos.X == railPos.X && pos.Z == railPos.Z)
                        return true;
                return false;
            }

            //CanConnectTo 已经连着或者本侧连接点还没满 对应原版 canConnectTo
            private bool CanConnectTo(RailState rail) => ConnectsTo(rail) || _connections.Count != 2;

            //ConnectTo 把对方接上并立刻重定形状 对应原版 connectTo
            private void ConnectTo(RailState rail)
            {
                _connections.Add(rail._pos);
                var north = _pos.Offset(Direction.North);
                var south = _pos.Offset(Direction.South);
                var west = _pos.Offset(Direction.West);
                var east = _pos.Offset(Direction.East);
                var n = HasConnection(north);
                var s = HasConnection(south);
                var w = HasConnection(west);
                var e = HasConnection(east);
                RailShape? shape = null;
                if (n || s) shape = RailShape.north_south;
                if (w || e) shape = RailShape.east_west;
                if (!_isStraight)
                {
                    if (s && e && !n && !w) shape = RailShape.south_east;
                    if (s && w && !n && !e) shape = RailShape.south_west;
                    if (n && w && !s && !e) shape = RailShape.north_west;
                    if (n && e && !s && !w) shape = RailShape.north_east;
                }
                if (shape == RailShape.north_south)
                {
                    if (IsRail(_level, north.Offset(Direction.Up))) shape = RailShape.ascending_north;
                    if (IsRail(_level, south.Offset(Direction.Up))) shape = RailShape.ascending_south;
                }
                if (shape == RailShape.east_west)
                {
                    if (IsRail(_level, east.Offset(Direction.Up))) shape = RailShape.ascending_east;
                    if (IsRail(_level, west.Offset(Direction.Up))) shape = RailShape.ascending_west;
                }
                shape ??= RailShape.north_south;
                _state = _state.SetValue(_block.RailShapeProperty, shape.Value);
                _level.SetBlock(_pos, _state, BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            }

            //HasNeighborRail 邻格有铁轨且愿意接过来 对应原版 hasNeighborRail
            private bool HasNeighborRail(BlockPos railPos)
            {
                var neighbor = GetRail(railPos);
                if (neighbor is null) return false;
                neighbor.RemoveSoftConnections();
                return neighbor.CanConnectTo(this);
            }

            //Place 重新定形状并让邻轨跟着接线 对应原版 place
            public RailState Place(bool hasSignal, bool first, RailShape defaultShape)
            {
                var north = _pos.Offset(Direction.North);
                var south = _pos.Offset(Direction.South);
                var west = _pos.Offset(Direction.West);
                var east = _pos.Offset(Direction.East);
                var n = HasNeighborRail(north);
                var s = HasNeighborRail(south);
                var w = HasNeighborRail(west);
                var e = HasNeighborRail(east);
                RailShape? shape = null;
                var northOrSouth = n || s;
                var westOrEast = w || e;
                if (northOrSouth && !westOrEast) shape = RailShape.north_south;
                if (westOrEast && !northOrSouth) shape = RailShape.east_west;
                var southAndEast = s && e;
                var southAndWest = s && w;
                var northAndEast = n && e;
                var northAndWest = n && w;
                if (!_isStraight)
                {
                    if (southAndEast && !n && !w) shape = RailShape.south_east;
                    if (southAndWest && !n && !e) shape = RailShape.south_west;
                    if (northAndWest && !s && !e) shape = RailShape.north_west;
                    if (northAndEast && !s && !w) shape = RailShape.north_east;
                }
                if (shape is null)
                {
                    if (northOrSouth && westOrEast) shape = defaultShape;
                    else if (northOrSouth) shape = RailShape.north_south;
                    else if (westOrEast) shape = RailShape.east_west;
                    //两端都连上时按有没有信号取相反的弯道 与原版两份顺序相反的判定一致
                    if (!_isStraight)
                    {
                        if (hasSignal)
                        {
                            if (southAndEast) shape = RailShape.south_east;
                            if (southAndWest) shape = RailShape.south_west;
                            if (northAndEast) shape = RailShape.north_east;
                            if (northAndWest) shape = RailShape.north_west;
                        }
                        else
                        {
                            if (northAndWest) shape = RailShape.north_west;
                            if (northAndEast) shape = RailShape.north_east;
                            if (southAndWest) shape = RailShape.south_west;
                            if (southAndEast) shape = RailShape.south_east;
                        }
                    }
                }
                if (shape == RailShape.north_south)
                {
                    if (IsRail(_level, north.Offset(Direction.Up))) shape = RailShape.ascending_north;
                    if (IsRail(_level, south.Offset(Direction.Up))) shape = RailShape.ascending_south;
                }
                if (shape == RailShape.east_west)
                {
                    if (IsRail(_level, east.Offset(Direction.Up))) shape = RailShape.ascending_east;
                    if (IsRail(_level, west.Offset(Direction.Up))) shape = RailShape.ascending_west;
                }
                shape ??= defaultShape;
                UpdateConnections(shape.Value);
                _state = _state.SetValue(_block.RailShapeProperty, shape.Value);
                if (!first && _level.GetBlockState(_pos) == _state) return this;
                _level.SetBlock(_pos, _state, BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
                foreach (var connection in _connections)
                {
                    var neighbor = GetRail(connection);
                    if (neighbor is null) continue;
                    neighbor.RemoveSoftConnections();
                    if (!neighbor.CanConnectTo(this)) continue;
                    neighbor.ConnectTo(this);
                }
                return this;
            }
        }
    }

    //RailBlock 普通铁轨 有弯道与上坡 对应原版 RailBlock
    public sealed class RailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("rail");

        //原版铁轨硬度 0.7 需要镐子
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        protected override bool IsStraight => false;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeAll;

        //顺序照 blocks.txt 的 shape|waterlogged
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["shape"] = BlockStateProperties.RailShapeAll,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };
    }

    //PoweredRailBlock 动力铁轨 通电后给矿车加速 对应原版 PoweredRailBlock
    //本作没有矿车 保留的是充能状态与沿轨道最多八格的充能传播
    public sealed class PoweredRailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("powered_rail");

        //原版动力铁轨硬度 0.7 需要镐子
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        protected override bool IsStraight => true;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeStraight;

        //顺序照 blocks.txt 的 powered|shape|waterlogged
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["powered"] = BlockStateProperties.Powered,
            ["shape"] = BlockStateProperties.RailShapeStraight,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        //UpdatePoweredState 自身有信号或者上下游能摸到供能轨就通电 对应原版 updateState
        protected override void UpdatePoweredState(ServerLevel level, BlockPos pos, BlockState state)
        {
            var isPowered = state.GetValue(BlockStateProperties.Powered);
            var shouldPower = level.HasNeighborSignal(pos)
                || FindPoweredRailSignal(level, pos, state, true, 0)
                || FindPoweredRailSignal(level, pos, state, false, 0);
            if (shouldPower == isPowered) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, shouldPower),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), state.Owner);
            if (state.GetValue(RailShapeProperty).IsSlope())
                level.UpdateNeighborsAt(pos.Offset(Direction.Up), state.Owner);
        }

        //FindPoweredRailSignal 沿轨往前找最多八格 对应原版 findPoweredRailSignal
        //上坡的前进方向会抬高一层 归到平轨形状再继续查
        private static bool FindPoweredRailSignal(ServerLevel level, BlockPos pos, BlockState state, bool forward,
            int searchDepth)
        {
            if (searchDepth >= 8) return false;
            var x = pos.X;
            var y = pos.Y;
            var z = pos.Z;
            var checkBelow = true;
            var shape = state.GetValue(BlockStateProperties.RailShapeStraight);
            switch (shape)
            {
                case RailShape.north_south:
                    if (forward) z++;
                    else z--;
                    break;
                case RailShape.east_west:
                    if (forward) x--;
                    else x++;
                    break;
                case RailShape.ascending_east:
                    if (forward) x--;
                    else { x++; y++; checkBelow = false; }
                    shape = RailShape.east_west;
                    break;
                case RailShape.ascending_west:
                    if (forward) { x--; y++; checkBelow = false; }
                    else x++;
                    shape = RailShape.east_west;
                    break;
                case RailShape.ascending_north:
                    if (forward) z++;
                    else { z--; y++; checkBelow = false; }
                    shape = RailShape.north_south;
                    break;
                case RailShape.ascending_south:
                    if (forward) { z++; y++; checkBelow = false; }
                    else z--;
                    shape = RailShape.north_south;
                    break;
            }
            if (IsSameRailWithPower(level, new BlockPos(x, y, z), forward, searchDepth, shape)) return true;
            return checkBelow && IsSameRailWithPower(level, new BlockPos(x, y - 1, z), forward, searchDepth, shape);
        }

        //IsSameRailWithPower 同向的动力铁轨且已充能 再看它的邻接信号或者继续往前找
        private static bool IsSameRailWithPower(ServerLevel level, BlockPos pos, bool forward, int searchDepth,
            RailShape direction)
        {
            if (level.GetBlockState(pos) is not { Owner: PoweredRailBlock } state) return false;
            var shape = state.GetValue(BlockStateProperties.RailShapeStraight);
            if (direction == RailShape.east_west && shape is RailShape.north_south
                or RailShape.ascending_north or RailShape.ascending_south) return false;
            if (direction == RailShape.north_south && shape is RailShape.east_west
                or RailShape.ascending_east or RailShape.ascending_west) return false;
            if (!state.GetValue(BlockStateProperties.Powered)) return false;
            if (level.HasNeighborSignal(pos)) return true;
            return FindPoweredRailSignal(level, pos, state, forward, searchDepth + 1);
        }
    }

    //DetectorRailBlock 探测铁轨 对应原版 DetectorRailBlock
    //原版靠压在轨上的矿车决定通电 本作没有矿车实体 只保留形状推导与朝上的直接信号
    public sealed class DetectorRailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("detector_rail");

        //原版探测铁轨硬度 0.7 需要镐子
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        //顺序照 blocks.txt 的 powered|shape|waterlogged
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["powered"] = BlockStateProperties.Powered,
            ["shape"] = BlockStateProperties.RailShapeStraight,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        protected override bool IsStraight => true;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeStraight;

        public override bool IsSignalSource => true;

        //GetDirectSignal 通电时只朝上方给满信号 对应原版 getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.Powered) && direction == Direction.Up ? 15 : 0;
    }

    //ActivatorRailBlock 激活铁轨 对应原版同名方块
    //原版通电后激活轨上的矿车 本作没有矿车 保留红石驱动的通电状态
    public sealed class ActivatorRailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("activator_rail");

        //原版激活铁轨硬度 0.7 需要镐子
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        //顺序照 blocks.txt 的 powered|shape|waterlogged
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["powered"] = BlockStateProperties.Powered,
            ["shape"] = BlockStateProperties.RailShapeStraight,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        protected override bool IsStraight => true;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeStraight;

        //UpdatePoweredState 按邻居信号写通电态 对应原版 updateState
        protected override void UpdatePoweredState(ServerLevel level, BlockPos pos, BlockState state)
        {
            var isPowered = state.GetValue(BlockStateProperties.Powered);
            var signal = level.HasNeighborSignal(pos);
            if (isPowered == signal) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, signal),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), state.Owner);
            if (state.GetValue(RailShapeProperty).IsSlope())
                level.UpdateNeighborsAt(pos.Offset(Direction.Up), state.Owner);
        }
    }
}
