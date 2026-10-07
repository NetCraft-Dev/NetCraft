using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Game.World.Phys.Collision;
using Direction = NetCraft.Primitives.Direction;
using DoorHingeSide = NetCraft.Registry.Enums.DoorHingeSide;
using DoubleBlockHalf = NetCraft.Registry.Enums.DoubleBlockHalf;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.Block;

//Doors, maps to vanilla net.minecraft.world.level.block.DoorBlock
//Occupies two cells vertically; the hinge side is decided by the walls on both sides and the click position at placement, opening follows the player facing and hinge
//Iron doors cannot be opened bare-handed; redstone only checks for a signal on itself and the other half
public static partial class Blocks
{
    public static readonly DoorBlock OAK_DOOR = new("oak_door", BlockSet.Wood);
    public static readonly DoorBlock IRON_DOOR = new("iron_door", BlockSet.Iron);
    public static readonly DoorBlock COPPER_DOOR = new("copper_door", BlockSet.Copper);

    //RegisterDoors registers doors into the real block table, registry name to material tier follows vanilla Blocks.java
    private static void RegisterDoors(Dictionary<string, BlockBehaviour> real)
    {
        RegisterDoor(real, OAK_DOOR);
        RegisterDoor(real, new DoorBlock("spruce_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("birch_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("jungle_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("acacia_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("dark_oak_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("pale_oak_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("mangrove_door", BlockSet.Wood));
        RegisterDoor(real, new DoorBlock("cherry_door", BlockSet.Cherry));
        RegisterDoor(real, new DoorBlock("bamboo_door", BlockSet.Bamboo));
        RegisterDoor(real, new DoorBlock("crimson_door", BlockSet.NetherWood));
        RegisterDoor(real, new DoorBlock("warped_door", BlockSet.NetherWood));
        RegisterDoor(real, IRON_DOOR);
        RegisterDoor(real, COPPER_DOOR);
        RegisterDoor(real, new DoorBlock("exposed_copper_door", BlockSet.Copper));
        RegisterDoor(real, new DoorBlock("weathered_copper_door", BlockSet.Copper));
        RegisterDoor(real, new DoorBlock("oxidized_copper_door", BlockSet.Copper));
        RegisterDoor(real, new DoorBlock("waxed_copper_door", BlockSet.Copper));
        RegisterDoor(real, new DoorBlock("waxed_exposed_copper_door", BlockSet.Copper));
        RegisterDoor(real, new DoorBlock("waxed_weathered_copper_door", BlockSet.Copper));
        RegisterDoor(real, new DoorBlock("waxed_oxidized_copper_door", BlockSet.Copper));
    }

    private static void RegisterDoor(Dictionary<string, BlockBehaviour> real, DoorBlock block)
        => real[block.Id.Path] = block;

    public sealed class DoorBlock : BlockBehaviour
    {
        //DoorShapes door panel shapes, sixteen by sixteen by three, maps to vanilla SHAPES
        private static readonly Dictionary<Direction, VoxelShape> DoorShapes =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(16.0, 13.0, 16.0));

        private readonly string _name;
        private readonly BlockSet _material;
        private readonly bool _canOpenByHand;
        private readonly SoundEvent _openSound;
        private readonly SoundEvent _closeSound;

        public DoorBlock(string name, BlockSet material)
        {
            _name = name;
            _material = material;
            //Iron doors cannot be opened bare-handed while the other tiers can, maps to vanilla BlockSetType.canOpenByHand
            _canOpenByHand = material != BlockSet.Iron;
            (_openSound, _closeSound) = SoundsOf(material);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //Wooden door hardness 3, iron 5, copper 3, maps to the strength of each vanilla tier
        public override float DestroySpeed => _material == BlockSet.Iron ? 5f : 3f;

        //Wood is mineable bare-handed, iron and copper need the correct tool
        public override bool RequiresCorrectToolForDrops
            => _material is BlockSet.Iron or BlockSet.Copper;

        //Properties states follow facing|half|hinge|open|powered in blocks.txt
        //The one in the table is a separately built property instance that GetValue cannot find; it must be declared here, and changing the order shifts the global state ids
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["half"] = BlockStateProperties.DoubleBlockHalfProperty,
            ["hinge"] = BlockStateProperties.DoorHinge,
            ["open"] = BlockStateProperties.Open,
            ["powered"] = BlockStateProperties.Powered,
        };

        //GetShape closed hugs the facing side, open rotates to the left or right of the facing per the hinge, maps to vanilla getShape
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
        {
            var facing = ToGeometry(state.GetValue(BlockStateProperties.HorizontalFacing));
            var doorDirection = state.GetValue(BlockStateProperties.Open)
                ? state.GetValue(BlockStateProperties.DoorHinge) == DoorHingeSide.right
                    ? facing.CounterClockWise
                    : facing.ClockWise
                : facing;
            return DoorShapes[doorDirection];
        }

        //GetStateForPlacement returns only the lower half, it fits only when the cell above can be replaced, the hinge side comes from both sides and the click position
        //A signal beside or above places it already open and powered, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection, Vec3 hitLocal)
        {
            var above = level.GetBlockState(pos.Offset(Direction.Up));
            if (above is not { Owner: BlockBehaviour aboveBlock } || !aboveBlock.CanBeReplaced) return null;
            var powered = level.HasNeighborSignal(pos) || level.HasNeighborSignal(pos.Offset(Direction.Up));
            return DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing, ToPropertyFacing(horizontalFacing))
                .SetValue(BlockStateProperties.DoorHinge, GetHinge(level, pos, horizontalFacing, hitLocal))
                .SetValue(BlockStateProperties.Powered, powered)
                .SetValue(BlockStateProperties.Open, powered)
                .SetValue(BlockStateProperties.DoubleBlockHalfProperty, DoubleBlockHalf.lower);
        }

        //SetPlacedBy places the upper half right after the lower one, maps to vanilla setPlacedBy
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
            => level.SetBlock(pos.Offset(Direction.Up),
                state.SetValue(BlockStateProperties.DoubleBlockHalfProperty, DoubleBlockHalf.upper),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);

        //UpdateShape the vertical neighbor changing must re-align with the other half and a lone half disappears, maps to vanilla updateShape
        //The lower half also disappears without support below
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            var half = state.GetValue(BlockStateProperties.DoubleBlockHalfProperty);
            var neighbourAbove = directionToNeighbour == Direction.Up;
            if ((directionToNeighbour == Direction.Up || directionToNeighbour == Direction.Down)
                && (half == DoubleBlockHalf.lower) == neighbourAbove)
            {
                //Realigns with the other half when it is a door half, otherwise this half has no reason to exist
                if (neighbourState.Owner is DoorBlock
                    && neighbourState.GetValue(BlockStateProperties.DoubleBlockHalfProperty) != half)
                    return neighbourState.SetValue(BlockStateProperties.DoubleBlockHalfProperty, half);
                return AIR.DefaultBlockState;
            }
            if (half == DoubleBlockHalf.lower && directionToNeighbour == Direction.Down
                && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        //CanSurvive the lower half needs support below and the upper needs itself below, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            var belowState = level.GetBlockState(below);
            if (state.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower)
                return belowState is { Owner: BlockBehaviour behaviour }
                    && behaviour.IsFaceSturdy(level, below, belowState.Value, Direction.Up);
            return belowState is { } lower && ReferenceEquals(lower.Owner, this);
        }

        //AffectNeighborsAfterRemoval removes the other half without drops when this one is gone, maps to the vanilla double-block drop suppression
        //Otherwise the remaining half would be destroyed by the shape update and drop again, dropping two doors at once (Mojira MC-188675)
        //Vanilla does this in playerWillDestroy covering only player breaks; here it is attached to the removal hook instead
        //Both player breaks and lost support are covered, and the whole door drops only once
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            var half = state.GetValue(BlockStateProperties.DoubleBlockHalfProperty);
            var otherPos = half == DoubleBlockHalf.lower ? pos.Offset(Direction.Up) : pos.Offset(Direction.Down);
            //The other half must be the other part of the same door; once it is no longer a door this stops, which also breaks the mutual erase recursion
            if (level.GetBlockState(otherPos) is not { } other
                || !ReferenceEquals(other.Owner, this)
                || other.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == half)
                return;
            //Writes the state without drops; vanilla preventDropFromBottomPart also replaces it with air using setBlock
            level.SetBlock(otherPos, AIR.DefaultBlockState,
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //UseOn bare-hand open and close, tiers that cannot be opened are rejected outright, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (!_canOpenByHand) return false;
            var updated = state.Cycle(BlockStateProperties.Open);
            //Vanilla only syncs the client without notifying neighbors; the shape update carries the new state to the other half so both turn together
            level.SetBlock(pos, updated, BlockUpdateFlags.Clients);
            PlaySound(level, pos, updated.GetValue(BlockStateProperties.Open));
            return true;
        }

        //NeighborChanged opens and marks powered when a signal appears on itself or the other half, maps to vanilla neighborChanged
        //Does nothing when the change is the door itself, so opening does not retrigger it
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            RegBlock changedBlock, bool movedByPiston)
        {
            if (ReferenceEquals(changedBlock, this)) return;
            var other = pos.Offset(state.GetValue(BlockStateProperties.DoubleBlockHalfProperty)
                == DoubleBlockHalf.lower ? Direction.Up : Direction.Down);
            var signal = level.HasNeighborSignal(pos) || level.HasNeighborSignal(other);
            if (signal == state.GetValue(BlockStateProperties.Powered)) return;
            if (signal != state.GetValue(BlockStateProperties.Open)) PlaySound(level, pos, signal);
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, signal)
                .SetValue(BlockStateProperties.Open, signal), BlockUpdateFlags.Clients);
        }

        //PlaySound open and close sound, the pitch jitters between 0.9 and 1.0, maps to vanilla playSound
        private void PlaySound(ServerLevel level, BlockPos pos, bool opening)
        {
            var sound = opening ? _openSound : _closeSound;
            level.PlaySound(sound, SoundSource.Blocks, pos, 1f, Random.Shared.NextSingle() * 0.1f + 0.9f);
        }

        //GetHinge hinge side decision, it first checks the left and right columns for blocking blocks and existing lower door halves, and only falls back to which side of the door the click is on
        //Maps to vanilla getHinge
        private static DoorHingeSide GetHinge(ServerLevel level, BlockPos pos, Direction placeDirection,
            Vec3 hitLocal)
        {
            var abovePos = pos.Offset(Direction.Up);
            var leftDirection = placeDirection.CounterClockWise;
            var leftPos = pos.Offset(leftDirection);
            var rightDirection = placeDirection.ClockWise;
            var rightPos = pos.Offset(rightDirection);
            var leftState = level.GetBlockState(leftPos);
            var leftAboveState = level.GetBlockState(abovePos.Offset(leftDirection));
            var rightState = level.GetBlockState(rightPos);
            var rightAboveState = level.GetBlockState(abovePos.Offset(rightDirection));
            var balance = (IsFullBlock(leftState, leftPos) ? -1 : 0)
                + (IsFullBlock(leftAboveState, abovePos.Offset(leftDirection)) ? -1 : 0)
                + (IsFullBlock(rightState, rightPos) ? 1 : 0)
                + (IsFullBlock(rightAboveState, abovePos.Offset(rightDirection)) ? 1 : 0);
            var doorLeft = IsLowerDoor(leftState);
            var doorRight = IsLowerDoor(rightState);
            if (doorLeft && !doorRight || balance > 0) return DoorHingeSide.right;
            if (doorRight && !doorLeft || balance < 0) return DoorHingeSide.left;
            var stepX = placeDirection.StepX;
            var stepZ = placeDirection.StepZ;
            var right = stepX < 0 && hitLocal.Z < 0.5 || stepX > 0 && hitLocal.Z > 0.5
                || stepZ < 0 && hitLocal.X > 0.5 || stepZ > 0 && hitLocal.X < 0.5;
            return right ? DoorHingeSide.right : DoorHingeSide.left;
        }

        //IsFullBlock whether this state's collision shape fills the block, maps to vanilla isCollisionShapeFullBlock
        //The shape only looks at the block itself, an empty world view suffices
        private static bool IsFullBlock(BlockState? state, BlockPos pos)
            => state is { Owner: BlockBehaviour behaviour }
                && behaviour.IsCollisionShapeFullBlock(state.Value, EmptyBlockGetter.Instance, pos);

        //IsLowerDoor whether this state is the lower half of another door, maps to doorLeft/doorRight in vanilla getHinge
        private static bool IsLowerDoor(BlockState? state)
            => state is { Owner: DoorBlock }
                && state.Value.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower;

        //SoundsOf the open and close sounds for a material tier, maps to doorOpen/doorClose of each vanilla BlockSetType tier
        private static (SoundEvent Open, SoundEvent Close) SoundsOf(BlockSet material) => material switch
        {
            BlockSet.Iron => (SoundEvents.IronDoorOpen, SoundEvents.IronDoorClose),
            BlockSet.Copper => (SoundEvents.CopperDoorOpen, SoundEvents.CopperDoorClose),
            BlockSet.Cherry => (SoundEvents.CherryWoodDoorOpen, SoundEvents.CherryWoodDoorClose),
            BlockSet.Bamboo => (SoundEvents.BambooWoodDoorOpen, SoundEvents.BambooWoodDoorClose),
            BlockSet.NetherWood => (SoundEvents.NetherWoodDoorOpen, SoundEvents.NetherWoodDoorClose),
            _ => (SoundEvents.WoodenDoorOpen, SoundEvents.WoodenDoorClose),
        };

        //ToPropertyFacing converts a geometry direction into the enum member used by block properties, doors only use the four horizontal ones
        private static NetCraft.Registry.Enums.Direction ToPropertyFacing(Direction direction)
        {
            if (direction == Direction.North) return NetCraft.Registry.Enums.Direction.north;
            if (direction == Direction.South) return NetCraft.Registry.Enums.Direction.south;
            if (direction == Direction.West) return NetCraft.Registry.Enums.Direction.west;
            return NetCraft.Registry.Enums.Direction.east;
        }

        //ToGeometry converts the property enum back to a geometry direction, only used to look up shape tables and rotate
        private static Direction ToGeometry(NetCraft.Registry.Enums.Direction direction) => direction switch
        {
            NetCraft.Registry.Enums.Direction.north => Direction.North,
            NetCraft.Registry.Enums.Direction.south => Direction.South,
            NetCraft.Registry.Enums.Direction.west => Direction.West,
            _ => Direction.East,
        };
    }
}
