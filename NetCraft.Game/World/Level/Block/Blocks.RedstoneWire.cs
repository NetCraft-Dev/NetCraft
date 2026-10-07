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

//Blocks redstone wire part; same class as Blocks.cs in a separate file to keep the main file short
//Redstone wire is the backbone of the whole redstone system; the connection model decides appearance and connections and power propagation decides how signals decay
public static partial class Blocks
{
    //RedstoneWireBlock redstone wire, maps to vanilla RedStoneWireBlock
    //Four connection states with three values each plus power 0-15, 1296 states in total
    //Signals only travel along connected directions and each wire takes the largest received signal minus one as its own power
    public sealed class RedstoneWireBlock : BlockBehaviour
    {
        //_shouldSignal temporarily turns off its own signal source flag while reading neighbor signals
        //Without turning it off getBestNeighborSignal would also count neighboring wire signals and create self-feedback
        private bool _shouldSignal = true;

        //_crossState the cross state with all four directions connected, the starting point for click toggling and shape recomputation, maps to vanilla crossState
        private BlockState? _crossState;

        //Shapes combine the four connection states, 3^4 of them computed once statically, maps to getShapeForEachState in vanilla makeShapes
        //A center dot plus four horizontal thin plates, with a vertical plate added on upward-climbing directions
        //Without it the shape falls back to a full block, sky light is treated as blocked and the wire's cell is darker than vanilla
        private static readonly Dictionary<(RedstoneSide, RedstoneSide, RedstoneSide, RedstoneSide), VoxelShape>
            ShapeTable = BuildShapeTable();

        private static Dictionary<(RedstoneSide, RedstoneSide, RedstoneSide, RedstoneSide), VoxelShape>
            BuildShapeTable()
        {
            //Center dot, 10 pixels wide and 1 pixel high
            var dot = NetCraft.Registry.Block.Column(10.0, 0.0, 1.0);
            //Horizontal extension plate stretching from the center to the half-block point in that direction
            var floor = Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(10.0, 0.0, 1.0, 0.0, 8.0));
            //Upward vertical plate, full height and against the inner side of that direction
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

        //Extend merges one direction's connection shape into the overall shape, stacking the vertical plate for upward states, maps to the vanilla switch
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

        //Vanilla redstone wire is instabreak with hardness 0, broken bare-handed instantly
        public override float DestroySpeed => 0f;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["east"] = BlockStateProperties.EastRedstone,
            ["north"] = BlockStateProperties.NorthRedstone,
            ["power"] = BlockStateProperties.Power,
            ["south"] = BlockStateProperties.SouthRedstone,
            ["west"] = BlockStateProperties.WestRedstone,
        };

        //Redstone wire is neither a conductor nor a full cube; vanilla uses noCollision to make the default predicate fail
        //Without the override NC's full-solid approximation would treat wire as a conductor and signals would never decay
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsFaceSturdy(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => false;

        //IsSignalSource is turned off while reading neighbors, maps to the vanilla shouldSignal field
        public override bool IsSignalSource => _shouldSignal;

        //The vanilla default state is NONE in all four directions with power 0
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.NorthRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.EastRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.SouthRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.WestRedstone, RedstoneSide.none)
                .SetValue(BlockStateProperties.Power, 0);

        //CrossState the cross state with all four directions connected, maps to vanilla crossState
        private BlockState CrossState => _crossState ??= DefaultBlockState
            .SetValue(BlockStateProperties.NorthRedstone, RedstoneSide.side)
            .SetValue(BlockStateProperties.EastRedstone, RedstoneSide.side)
            .SetValue(BlockStateProperties.SouthRedstone, RedstoneSide.side)
            .SetValue(BlockStateProperties.WestRedstone, RedstoneSide.side);

        //CanSurvive the upward face of the cell below is sturdy or the block itself is a hopper, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            return level.GetBlockState(below) is { } support && CanSurviveOn(level, below, support);
        }

        //CanSurviveOn whether it can support redstone wire, maps to vanilla canSurviveOn
        //Vanilla lets trapdoors through separately; trapdoors are still placeholders here and the full-solid approximation already covers them, so they are not listed
        private static bool CanSurviveOn(ServerLevel level, BlockPos supportPos, BlockState supportState)
            => IsFaceSturdyAt(level, supportPos, supportState, Direction.Up)
                || supportState.Owner.Id == RedstoneIds.Hopper;

        //GetStateForPlacement computes the connections from the surroundings on placement, maps to the vanilla method of the same name
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing) => GetConnectionState(level, CrossState, pos);

        //GetConnectionState fills in missing connections then adds SIDEs for a cross or dot, maps to the vanilla method of the same name
        //A dot has no connections and a cross has all four; these two toggle each other on click so they are handled separately
        private static BlockState GetConnectionState(ServerLevel level, BlockState state, BlockPos pos)
        {
            var wasDot = IsDot(state);
            var computed = GetMissingConnections(level, DefaultWireStateWithPower(state), pos);
            if (wasDot && IsDot(computed)) return computed;
            var northSouthEmpty = !computed.GetValue(BlockStateProperties.NorthRedstone).IsConnected()
                && !computed.GetValue(BlockStateProperties.SouthRedstone).IsConnected();
            var eastWestEmpty = !computed.GetValue(BlockStateProperties.EastRedstone).IsConnected()
                && !computed.GetValue(BlockStateProperties.WestRedstone).IsConnected();
            //Empty north and south fill in east, south and west; empty east and west fill in north and south; what is added is a SIDE, not UP
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

        //DefaultWireStateWithPower takes the default state but keeps the passed state's power, maps to the two setValue(POWER, ...) calls in vanilla
        private static BlockState DefaultWireStateWithPower(BlockState state)
            => REDSTONE_WIRE.DefaultBlockState.SetValue(BlockStateProperties.Power,
                state.GetValue(BlockStateProperties.Power));

        //GetMissingConnections recomputes the connections not yet made in the four directions, maps to the vanilla method of the same name
        //canConnectUp decides whether connections can be raised to UP this round; if the block above is a conductor the wire cannot climb
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

        //GetConnectingSide computes what a side should connect as, maps to the two vanilla overloads
        private static RedstoneSide GetConnectingSide(ServerLevel level, BlockPos pos, Direction direction)
            => GetConnectingSide(level, pos, direction, !IsRedstoneConductor(level, pos.Offset(Direction.Up)));

        private static RedstoneSide GetConnectingSide(ServerLevel level, BlockPos pos, Direction direction,
            bool canConnectUp)
        {
            var neighbourPos = pos.Offset(direction);
            if (level.GetBlockState(neighbourPos) is not { } neighbour) return RedstoneSide.none;
            if (canConnectUp)
            {
                //If wire can be placed above the neighbor and the cell above also connects, the connection is raised to UP
                //When it cannot stand above it falls back to SIDE, maps to the vanilla isPlaceableAbove branch
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

        //ShouldConnectTo whether the block can connect with redstone wire, maps to the two vanilla overloads
        //Wire always connects, repeaters connect on both ports, observers connect only on their output side and other signal sources also count as connected
        private static bool ShouldConnectTo(BlockState? state, Direction? direction = null)
        {
            if (state is not { } block) return false;
            if (block.Owner.Id == RedstoneIds.Wire) return true;
            if (block.Owner.Id != RedstoneIds.Repeater)
            {
                //An observer connects only toward its FACING side; the observer lands in P2-6 and was a placeholder without this property before
                if (block.Owner.Id == RedstoneIds.Observer)
                    return direction is { } side
                        && block.HasProperty(BlockStateProperties.FacingProperty)
                        && block.GetValue(BlockStateProperties.FacingProperty).ToPrimitive() == side;
                return direction is not null && block.Owner is IBlockSignalBehaviour { IsSignalSource: true };
            }
            var repeaterDirection = block.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            return repeaterDirection == direction || repeaterDirection.Opposite == direction;
        }

        //UpdateShape recomputes the whole connection on a vertical change and recomputes as needed or entirely on a side change, maps to the vanilla method of the same name
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Down)
                return CanSurviveOn(level, neighbourPos, neighbourState) ? state : AIR.DefaultBlockState;
            if (directionToNeighbour == Direction.Up)
                return GetConnectionState(level, state, pos);
            var property = SideProperty(directionToNeighbour);
            var sideConnection = GetConnectingSide(level, pos, directionToNeighbour);
            //When the connection did not change and it was already a cross, changing only this side's value is enough
            if (sideConnection.IsConnected() == state.GetValue(property).IsConnected() && !IsCross(state))
                return state.SetValue(property, sideConnection);
            var baseState = CrossState.SetValue(BlockStateProperties.Power, state.GetValue(BlockStateProperties.Power));
            return GetConnectionState(level, baseState.SetValue(property, sideConnection), pos);
        }

        //UpdateIndirectNeighbourShapes corner coupling, maps to the vanilla method of the same name
        //When the neighbor is not wire, the diagonal wires above and below it must recompute their connections, this is the wire climbing up and down
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

        //ShapeUpdateCorner sends a shape update to the diagonal cell when it is wire, maps to the two duplicated blocks in vanilla
        private static void ShapeUpdateCorner(ServerLevel level, BlockPos cornerPos, Direction direction,
            int updateFlags, int updateLimit)
        {
            if (level.GetBlockState(cornerPos)?.Owner.Id != RedstoneIds.Wire) return;
            var sourcePos = cornerPos.Offset(direction.Opposite);
            level.NeighborShapeChanged(direction.Opposite, cornerPos, sourcePos,
                level.GetBlockState(sourcePos) ?? AIR.DefaultBlockState, updateFlags, updateLimit);
        }

        //GetSignal gives signals only in connected directions and always upward, maps to the vanilla method of the same name
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
        {
            if (!_shouldSignal || direction == Direction.Down) return 0;
            var power = OwnSignal(level, pos, state);
            if (power == 0) return 0;
            if (direction == Direction.Up) return power;
            //The querier is on the opposite side of direction, so what matters is whether that side is connected
            var property = SideProperty(direction.Opposite);
            return GetConnectionState(level, state, pos).GetValue(property).IsConnected() ? power : 0;
        }

        //GetDirectSignal matches its own signal, maps to the vanilla method of the same name
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => _shouldSignal ? GetSignal(level, pos, state, direction) : 0;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        //GetBlockSignal the strongest signal from neighbors; its own signal source flag is turned off before reading, maps to the vanilla method of the same name
        //Without turning it off, neighboring wire would feed its own power back as a new signal source and wire would never decay
        public int GetBlockSignal(ServerLevel level, BlockPos pos)
        {
            _shouldSignal = false;
            var signal = level.GetBestNeighborSignal(pos);
            _shouldSignal = true;
            return signal;
        }

        //UpdatePowerStrength computes the target power and writes it back, maps to vanilla DefaultRedstoneWireEvaluator
        //The write-back uses only the Clients flag; after a change this block and the neighbors of all six directions must recompute
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

        //CalculateTargetStrength takes the max of neighbor signals and signals from adjacent wire, maps to the vanilla method of the same name
        private int CalculateTargetStrength(ServerLevel level, BlockPos pos)
        {
            var blockSignal = GetBlockSignal(level, pos);
            if (blockSignal == 15) return blockSignal;
            return Math.Max(blockSignal, GetIncomingWireSignal(level, pos));
        }

        //GetIncomingWireSignal takes the wire power of the four neighboring cells plus their upper and lower diagonals, subtracting one and taking the max, maps to the vanilla method of the same name
        //Subtracting one is a single level of decay per block; the two diagonal cells bypass that decay and whether they apply is decided by whether the neighbor is a conductor
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

        //GetWireSignal takes the power of that cell when it is wire, maps to the vanilla method of the same name
        private static int GetWireSignal(BlockState? state)
            => state is { } wire && wire.Owner.Id == RedstoneIds.Wire
                ? wire.GetValue(BlockStateProperties.Power)
                : 0;

        //NeighborChanged recomputes the power on a neighbor change and drops when it cannot stand, maps to the vanilla method of the same name
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

        //OnPlace computes the power once then notifies the wire above, below and around, maps to the vanilla method of the same name
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdatePowerStrength(level, pos, state);
            level.UpdateNeighborsAt(pos.Offset(Direction.Up), this);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), this);
            UpdateNeighborsOfNeighboringWires(level, pos);
        }

        //AffectNeighborsAfterRemoval all six directions must recompute after removal, maps to the vanilla method of the same name
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
            UpdatePowerStrength(level, pos, state);
            UpdateNeighborsOfNeighboringWires(level, pos);
        }

        //UpdateNeighborsOfNeighboringWires the corners of surrounding wire must move too, maps to the vanilla method of the same name
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

        //CheckCornerChangeAt recomputes this block and the six neighbors when that cell is wire, maps to the vanilla method of the same name
        private void CheckCornerChangeAt(ServerLevel level, BlockPos pos)
        {
            if (level.GetBlockState(pos)?.Owner.Id != RedstoneIds.Wire) return;
            level.UpdateNeighborsAt(pos, this);
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //UseOn right click toggles between cross and dot, maps to vanilla useWithoutItem
        //Vanilla only acts when it was already a cross or dot; clicking a normal corner shape does nothing
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

        //UpdatesOnShapeChange the directions whose connection state changed must notify their support blocks, maps to the vanilla method of the same name
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

        //IsCross all four directions connected, maps to the vanilla method of the same name
        private static bool IsCross(BlockState state)
            => state.GetValue(BlockStateProperties.NorthRedstone).IsConnected()
                && state.GetValue(BlockStateProperties.SouthRedstone).IsConnected()
                && state.GetValue(BlockStateProperties.EastRedstone).IsConnected()
                && state.GetValue(BlockStateProperties.WestRedstone).IsConnected();

        //IsDot no direction connected, maps to the vanilla method of the same name
        private static bool IsDot(BlockState state)
            => !state.GetValue(BlockStateProperties.NorthRedstone).IsConnected()
                && !state.GetValue(BlockStateProperties.SouthRedstone).IsConnected()
                && !state.GetValue(BlockStateProperties.EastRedstone).IsConnected()
                && !state.GetValue(BlockStateProperties.WestRedstone).IsConnected();

        //HorizontalDirections the four horizontal directions in the order of vanilla Direction.Plane.HORIZONTAL
        //Redstone is sensitive to update order, it cannot be reordered by axis
        private static readonly Direction[] HorizontalDirections =
        {
            Direction.North, Direction.South, Direction.West, Direction.East,
        };

        //SideProperty gets the matching connection property for a horizontal direction, maps to vanilla PROPERTY_BY_DIRECTION
        //Only defined for horizontal directions and call sites must filter first, matching vanilla EnumMap falling to null
        private static EnumProperty<RedstoneSide> SideProperty(Direction direction) => direction.Id3D switch
        {
            Direction.NorthId => BlockStateProperties.NorthRedstone,
            Direction.SouthId => BlockStateProperties.SouthRedstone,
            Direction.WestId => BlockStateProperties.WestRedstone,
            _ => BlockStateProperties.EastRedstone,
        };

        //IsRedstoneConductor whether that cell is a redstone conductor, maps to vanilla BlockState.isRedstoneConductor
        private static bool IsRedstoneConductor(ServerLevel level, BlockPos pos)
            => level.GetBlockState(pos) is { } state
                && state.Owner is IBlockSignalBehaviour behaviour
                && behaviour.IsRedstoneConductor(level, pos, state);

        //IsFaceSturdyAt whether that cell's face toward a direction is sturdy enough, maps to vanilla BlockState.isFaceSturdy
        //Face sturdiness is not in the signal contract and only the block behavior knows it, so it is taken from BlockBehaviour
        //The name avoids the IsFaceSturdy overridden by this class
        private static bool IsFaceSturdyAt(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.Owner is BlockBehaviour behaviour && behaviour.IsFaceSturdy(level, pos, state, direction);
    }

    public static readonly RedstoneWireBlock REDSTONE_WIRE = new();
}
