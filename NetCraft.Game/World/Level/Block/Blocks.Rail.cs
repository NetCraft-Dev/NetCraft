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

//The four rail types, maps to vanilla RailBlock / PoweredRailBlock / DetectorRailBlock / ActivatorRailBlock
//Shape derivation follows RailState: split the current shape into two connection points and re-decide the shape from whether there are rails in the four neighbors
//Powered rails propagate power up to eight blocks along the track; the minecart actions of detector and activator rails depend on the minecart entity, which is not wired up
public static partial class Blocks
{
    public static readonly RailBlock RAIL = new();
    public static readonly PoweredRailBlock POWERED_RAIL = new();
    public static readonly DetectorRailBlock DETECTOR_RAIL = new();
    public static readonly ActivatorRailBlock ACTIVATOR_RAIL = new();

    //RegisterRails registers the four rail types into the real block table
    private static void RegisterRails(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { RAIL, POWERED_RAIL, DETECTOR_RAIL, ACTIVATOR_RAIL };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //BaseRail rail base class, maps to vanilla BaseRailBlock
    //Flat is two pixels high and a slope eight; it drops when support is gone or the raised side is empty
    public abstract class BaseRail : BlockBehaviour
    {
        private static readonly VoxelShape FlatShape = NetCraft.Registry.Block.Column(16.0, 0.0, 2.0);
        private static readonly VoxelShape SlopeShape = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);

        //IsStraight whether it is a straight rail, only normal rails can curve
        protected abstract bool IsStraight { get; }

        //RailShapeProperty shape property instance used by this block, six straight and ten curved
        protected abstract EnumProperty<RailShape> RailShapeProperty { get; }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(RailShapeProperty).IsSlope() ? SlopeShape : FlatShape;

        //CanSurvive requires support below, maps to vanilla canSupportRigidBlock
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
            => CanSupportRigid(level, pos.Offset(Direction.Down));

        //OnPlace recomputes the shape the moment it becomes a rail, maps to vanilla onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdateState(level, pos, state, movedByPiston);
        }

        //NeighborChanged drops when support is gone, otherwise recomputes the shape, maps to vanilla neighborChanged
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

        //AffectNeighborsAfterRemoval notifies above and below after removal, maps to vanilla affectNeighborsAfterRemoval
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

        //GetStateForPlacement an east-west facing gives east-west, otherwise north-south, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
        {
            var eastWest = horizontalFacing == Direction.East || horizontalFacing == Direction.West;
            return DefaultBlockState
                .SetValue(RailShapeProperty, eastWest ? RailShape.east_west : RailShape.north_south)
                .SetValue(BlockStateProperties.Waterlogged, false);
        }

        //UpdateState recomputes the shape from connections, then straight rails run their own powered check
        //Vanilla re-enters via level.neighborChanged while this calls directly, avoiding re-queuing inside the SetBlock flow
        protected virtual void UpdateState(ServerLevel level, BlockPos pos, BlockState state, bool movedByPiston)
        {
            var shaped = UpdateDir(level, pos, state, true);
            if (IsStraight) UpdatePoweredState(level, pos, shaped);
        }

        //UpdatePoweredState powered check for straight rails, maps to the four-argument vanilla updateState overridden by subclasses
        protected virtual void UpdatePoweredState(ServerLevel level, BlockPos pos, BlockState state) { }

        //UpdateDir recomputes the shape via RailState, maps to vanilla updateDir
        private BlockState UpdateDir(ServerLevel level, BlockPos pos, BlockState state, bool first)
        {
            var current = state.GetValue(RailShapeProperty);
            return new RailState(level, pos, state).Place(level.HasNeighborSignal(pos), first, current).State;
        }

        //ShouldBeRemoved support is gone or the raised side is unsupported, maps to vanilla shouldBeRemoved
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

        //CanSupportRigid whether the cell can support a rail, maps to vanilla canSupportRigidBlock
        //The empty world view suffices for the shape; rails need the RIGID tier (the outer frame supports it)
        private static bool CanSupportRigid(ServerLevel level, BlockPos pos)
        {
            if (level.GetBlockState(pos) is not { } state) return false;
            return SupportType.Rigid.IsSupporting(state, EmptyBlockGetter.Instance, pos, Direction.Up);
        }

        //IsRail whether the cell is a rail, maps to vanilla isRail(level, pos) which only checks that cell
        //Used for slope checks; an earlier mistaken three-step lookup also counted rails below and misjudged flat rails as slopes
        private static bool IsRail(ServerLevel level, BlockPos pos)
            => level.GetBlockState(pos) is { Owner: BaseRail };

        //RailState rail connection derivation, maps to vanilla RailState
        //The shape determines two connection points; a connectable rail at a point keeps it, then the shape is re-decided from the four neighbors
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

            //UpdateConnections lays the two connection points into adjacent cells per the shape, maps to vanilla updateConnections
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

            //RemoveSoftConnections drops a connection point with no connectable rail, maps to vanilla removeSoftConnections
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

            //GetRail finds a rail at this position plus the cells above and below, maps to vanilla getRail
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

            //HasConnection whether a connection point contains this cell, comparing only horizontal coordinates, maps to vanilla hasConnection
            private bool HasConnection(BlockPos railPos)
            {
                foreach (var pos in _connections)
                    if (pos.X == railPos.X && pos.Z == railPos.Z)
                        return true;
                return false;
            }

            //CanConnectTo already connected or this side's connection point is not full, maps to vanilla canConnectTo
            private bool CanConnectTo(RailState rail) => ConnectsTo(rail) || _connections.Count != 2;

            //ConnectTo connects the other and immediately re-decides the shape, maps to vanilla connectTo
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

            //HasNeighborRail a rail is in a neighboring cell and is willing to connect, maps to vanilla hasNeighborRail
            private bool HasNeighborRail(BlockPos railPos)
            {
                var neighbor = GetRail(railPos);
                if (neighbor is null) return false;
                neighbor.RemoveSoftConnections();
                return neighbor.CanConnectTo(this);
            }

            //Place re-decides the shape and makes neighbor rails connect, maps to vanilla place
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
                    //With both ends connected it picks the opposite curve by whether there is a signal, matching the two reversed checks in vanilla
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

    //RailBlock normal rail with curves and slopes, maps to vanilla RailBlock
    public sealed class RailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("rail");

        //Vanilla rail hardness 0.7 needs a pickaxe
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        protected override bool IsStraight => false;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeAll;

        //The order follows shape|waterlogged in blocks.txt
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["shape"] = BlockStateProperties.RailShapeAll,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };
    }

    //PoweredRailBlock powered rail that accelerates minecarts when powered, maps to vanilla PoweredRailBlock
    //This project has no minecarts, only the powered state and power propagation up to eight blocks along the track are kept
    public sealed class PoweredRailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("powered_rail");

        //Vanilla powered rail hardness 0.7 needs a pickaxe
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        protected override bool IsStraight => true;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeStraight;

        //The order follows powered|shape|waterlogged in blocks.txt
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["powered"] = BlockStateProperties.Powered,
            ["shape"] = BlockStateProperties.RailShapeStraight,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        //UpdatePoweredState powers up when it has a signal or can reach a powered rail upstream or downstream, maps to vanilla updateState
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

        //FindPoweredRailSignal searches up to eight blocks forward along the track, maps to vanilla findPoweredRailSignal
        //The forward direction of a slope rises one level, folds into a flat rail shape and continues searching
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

        //IsSameRailWithPower a same-facing powered rail that is already powered, then it checks its neighbor signals or keeps searching forward
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

    //DetectorRailBlock detector rail, maps to vanilla DetectorRailBlock
    //Vanilla powers it from a minecart on the rail; this project has no minecart entity, so only the shape derivation and the upward direct signal are kept
    public sealed class DetectorRailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("detector_rail");

        //Vanilla detector rail hardness 0.7 needs a pickaxe
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        //The order follows powered|shape|waterlogged in blocks.txt
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["powered"] = BlockStateProperties.Powered,
            ["shape"] = BlockStateProperties.RailShapeStraight,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        protected override bool IsStraight => true;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeStraight;

        public override bool IsSignalSource => true;

        //GetDirectSignal gives a full signal upward only when powered, maps to vanilla getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.Powered) && direction == Direction.Up ? 15 : 0;
    }

    //ActivatorRailBlock activator rail, maps to the vanilla block of the same name
    //Vanilla activates minecarts on the rail when powered; this project has no minecarts, so only the redstone-driven powered state is kept
    public sealed class ActivatorRailBlock : BaseRail
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("activator_rail");

        //Vanilla activator rail hardness 0.7 needs a pickaxe
        public override float DestroySpeed => 0.7f;
        public override bool RequiresCorrectToolForDrops => true;

        //The order follows powered|shape|waterlogged in blocks.txt
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["powered"] = BlockStateProperties.Powered,
            ["shape"] = BlockStateProperties.RailShapeStraight,
            ["waterlogged"] = BlockStateProperties.Waterlogged,
        };

        protected override bool IsStraight => true;
        protected override EnumProperty<RailShape> RailShapeProperty => BlockStateProperties.RailShapeStraight;

        //UpdatePoweredState writes the powered state from neighbor signals, maps to vanilla updateState
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
