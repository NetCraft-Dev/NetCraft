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

//Tripwire family, maps to vanilla net.minecraft.world.level.block.TripWireHookBlock and TripWireBlock
//Tripwire hooks mount on walls as anchors and tripwire spans between two points; an entity stepping on it makes the hook output strength 15
//The two call each other to compute state: the hook scans the whole line and the wire notifies the hook when stepped on
public static partial class Blocks
{
    public static readonly TripWireHookBlock TRIPWIRE_HOOK = new();
    public static readonly TripWireBlock TRIPWIRE = new();

    //RegisterTripwire registers the tripwire family into the real block table
    private static void RegisterTripwire(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { TRIPWIRE_HOOK, TRIPWIRE };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //TripWireHookBlock tripwire hook, maps to vanilla TripWireHookBlock
    //Scans up to 42 blocks on the facing side; it counts as connected only if the other end has a hook facing it, then powered depends on whether the line is stepped on
    public sealed class TripWireHookBlock : BlockBehaviour
    {
        //WireDistMax maximum length of a tripwire, maps to vanilla WIRE_DIST_MAX
        public const int WireDistMax = 42;

        //RecheckPeriod recheck interval while stepped on, maps to vanilla RECHECK_PERIOD
        public const int RecheckPeriod = 10;

        //Hook shape, six pixels wide and ten high, attached to the back of the facing, maps to vanilla SHAPES
        private static readonly Dictionary<Direction, VoxelShape> HookShapes =
            Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(6.0, 0.0, 10.0, 10.0, 16.0));

        public override Identifier Id => Identifier.WithDefaultNamespace("tripwire_hook");

        //Vanilla tripwire hook hardness 0, breaks on touch
        public override float DestroySpeed => 0f;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => HookShapes[state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];

        //CanSurvive the back of the facing needs a sturdy face to support it, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var behind = pos.Offset(direction.Opposite);
            return level.GetBlockState(behind) is { } support
                && support.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, behind, support, direction);
        }

        //UpdateShape drops when the supporting block is gone, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            var facing = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            if (directionToNeighbour == facing.Opposite && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        //GetStateForPlacement the facing is the back of the player's horizontal facing and it cannot be placed without attachment, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
        {
            var state = DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing, horizontalFacing.Opposite.ToState());
            return CanSurvive(level, pos, state) ? state : null;
        }

        //SetPlacedBy rechecks the whole line on placement, maps to vanilla setPlacedBy
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
            => CalculateState(level, pos, state, false, false, -1, null);

        //Tick periodically rechecks, which sustains the line while stepped on, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
            => CalculateState(level, pos, state, false, true, -1, null);

        //AffectNeighborsAfterRemoval lets the whole line know before the hook is removed, maps to vanilla affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            OnRemoved(level, pos, state);
        }

        public override bool IsSignalSource => true;

        //OwnSignal gives full when powered, maps to vanilla ownSignal
        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        //GetDirectSignal only gives toward the facing side, maps to vanilla getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.Powered)
                && state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction
                ? 15
                : 0;

        //CalculateState scans the whole line from the hook and recomputes attached and powered, maps to the vanilla method of the same name
        //isBeingDestroyed true means the hook is being removed, in which case it no longer writes itself back
        //wireSource is which cell on the line triggered this; that cell takes part in the computation with the passed state instead of reading from the world
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
                //It counts as one line only if the other end also has a hook facing it
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
            //It counts as attached only when both ends are connected; one end alone cannot pull it taut, maps to vanilla attached &= receiverPos > 1
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

            //When the attached state changes the whole line's attached is updated so the wire knows it is taut
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

        //OnRemoved cleanup when the hook is removed, maps to vanilla onRemoved
        //Vanilla also plays a detach sound here; block behaviors have no sound output, so it is deferred along with levers and buttons until the sound output opens up
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

    //TripWireBlock tripwire, maps to vanilla TripWireBlock
    //It counts as taut (attached) only when both ends connect to hooks; an entity stepping on the line sets powered and notifies the hooks
    public sealed class TripWireBlock : BlockBehaviour
    {
        //Recheck interval while stepped on, maps to vanilla RECHECK_PERIOD
        private const int RecheckPeriod = 10;

        //Two shapes for taut and slack; when taut the line is pulled tight under its own cell, maps to vanilla SHAPE_ATTACHED/SHAPE_NOT_ATTACHED
        private static readonly VoxelShape AttachedShape = NetCraft.Registry.Block.Column(16.0, 1.0, 2.5);
        private static readonly VoxelShape DroppedShape = NetCraft.Registry.Block.Column(16.0, 0.0, 8.0);

        //Entity detection boxes corresponding one to one with the two shapes, pixels converted to blocks, maps to the shape bounding boxes used in vanilla checkPressed
        private static readonly AABB AttachedBox = new(0.0, 1 / 16.0, 0.0, 1.0, 2.5 / 16.0, 1.0);
        private static readonly AABB DroppedBox = new(0.0, 0.0, 0.0, 1.0, 0.5, 1.0);

        public override Identifier Id => Identifier.WithDefaultNamespace("tripwire");

        //Vanilla tripwire hardness 0, breaks on touch
        public override float DestroySpeed => 0f;

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => state.GetValue(BlockStateProperties.Attached) ? AttachedShape : DroppedShape;

        //GetStateForPlacement computes the four connections from the four neighbors, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState
                .SetValue(BlockStateProperties.NorthConnected, Connects(level, pos, Direction.North))
                .SetValue(BlockStateProperties.EastConnected, Connects(level, pos, Direction.East))
                .SetValue(BlockStateProperties.SouthConnected, Connects(level, pos, Direction.South))
                .SetValue(BlockStateProperties.WestConnected, Connects(level, pos, Direction.West));

        //UpdateShape recomputes that direction's connection when a horizontal neighbor changes, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (!directionToNeighbour.IsHorizontal) return state;
            var property = PropertyFor(directionToNeighbour);
            return state.SetValue(property, ShouldConnectTo(neighbourState, directionToNeighbour));
        }

        //OnPlace makes the hooks at both ends recheck once right after placement, maps to vanilla onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            UpdateSource(level, pos, state);
        }

        //AffectNeighborsAfterRemoval removing the line breaks the circuit and notifies the hooks at both ends to recompute, maps to vanilla affectNeighborsAfterRemoval
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (movedByPiston) return;
            UpdateSource(level, pos, state.SetValue(BlockStateProperties.Powered, true));
        }

        //PlayerDestroy marks this cell as cut before breaking the line with shears, maps to vanilla playerWillDestroy
        //The break happens after this callback, so a hook rechecking in the same tick still reads this cell as DISARMED=true
        //Vanilla also fires GameEventSHEAR here; this project has no game event channel, so the sound output is what matters
        public override void PlayerDestroy(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state)
        {
            var held = player.Inventory.GetSelectedItem();
            if (held.IsEmpty() || held.GetItem().Id.Path != "shears") return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Disarmed, true),
                BlockUpdateFlags.SkipBlockEntitySideEffects | BlockUpdateFlags.Invisible);
        }

        //Tick rechecks periodically while stepped on and releases when the entity leaves, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) return;
            CheckPressed(level, pos, state);
        }

        //OnEntityInside rechecks immediately when an entity enters the wire's cell, maps to vanilla entityInside
        //Vanilla passes the stepping entity in directly; hooks here only give a position, so entities are always queried by shape range
        public override void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (state.GetValue(BlockStateProperties.Powered) || level.HasScheduledTick(pos, this)) return;
            CheckPressed(level, pos, state);
        }

        //ShouldConnectTo whether the block can connect with tripwire, maps to the vanilla method of the same name
        //The tripwire hook at the other end must face toward itself, in the opposite direction
        public bool ShouldConnectTo(BlockState? other, Direction direction)
        {
            if (other is not { } state) return false;
            if (ReferenceEquals(state.Owner, TRIPWIRE_HOOK))
                return state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction.Opposite;
            return ReferenceEquals(state.Owner, TRIPWIRE);
        }

        //PropertyFor maps direction to connection property, only the four horizontal directions are handled
        //Direction is a struct not an enum so switch pattern matching cannot be used, only per-item comparison
        private static BooleanProperty PropertyFor(Direction direction)
        {
            if (direction == Direction.North) return BlockStateProperties.NorthConnected;
            if (direction == Direction.East) return BlockStateProperties.EastConnected;
            if (direction == Direction.South) return BlockStateProperties.SouthConnected;
            return BlockStateProperties.WestConnected;
        }

        //Connects reads the neighboring block to decide whether that direction is connected
        private static bool Connects(ServerLevel level, BlockPos pos, Direction direction)
        {
            var neighbourPos = pos.Offset(direction);
            var neighbour = level.GetBlockState(neighbourPos);
            if (neighbour is not { } state) return false;
            if (ReferenceEquals(state.Owner, TRIPWIRE_HOOK))
                return state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction.Opposite;
            return ReferenceEquals(state.Owner, TRIPWIRE);
        }

        //CheckPressed re-evaluates powered from entities within the shape range, writes back on change and makes the hooks at both ends recompute
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

        //UpdateSource scans south and west once each and makes a hook it runs into recompute the whole line, maps to vanilla updateSource
        //Only two directions are scanned because the other two are necessarily covered from the opposite side, two directions suffice to cover the whole line
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
