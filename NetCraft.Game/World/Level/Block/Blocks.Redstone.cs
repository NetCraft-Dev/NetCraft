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
//Util also has a Random namespace that clashes with System.Random, so only Mth is imported
using Mth = NetCraft.Util.Mth;

namespace NetCraft.Game.World.Level.Block;

//Blocks redstone components part; same class as Blocks.cs in a separate file to keep the main file short
//Property names and value order always follow blocks.txt; a misplaced property shifts the global BlockState ids with it
public static partial class Blocks
{
    //RedstoneBlock redstone block, a constant strength 15 signal source with no properties
    public sealed class RedstoneBlock : BlockBehaviour
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_block");
        //Vanilla redstone block hardness 5 needs a pickaxe
        public override float DestroySpeed => 5f;
        public override bool RequiresCorrectToolForDrops => true;
        public override bool IsSignalSource => true;

        //A redstone block gives a full 15 in every direction, maps to vanilla getSignal
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction) => 15;

        //A redstone block does not act as a direct signal source; the block's direct signal comes from the base, maps to vanilla not overriding getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => 0;
    }

    //FaceAttachedHorizontalDirectionalBlock face-attached block base class shared by levers and buttons, maps to the vanilla class of the same name
    //FACE decides floor, ceiling or wall attachment; FACING is the horizontal facing and points away from the support when on a wall
    public abstract class FaceAttachedHorizontalDirectionalBlock : BlockBehaviour
    {
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["face"] = BlockStateProperties.AttachFaceProperty,
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["powered"] = BlockStateProperties.Powered,
        };

        //Face-attached blocks are not conductors, signals only travel via the direct signal path
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        //GetDirectSignal only gives toward the attachment direction when powered, maps to vanilla getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.Powered) && GetConnectedDirection(state) == direction ? 15 : 0;

        //The vanilla constructor specifies the default state explicitly; without it you land on the face=floor combination
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Powered, false)
                .SetValue(BlockStateProperties.AttachFaceProperty, AttachFace.wall);

        //GetStateForPlacement clicking the top stands on the floor, clicking the ceiling hangs from above and clicking a side attaches to the wall, maps to the vanilla method of the same name
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
                    //On a wall FACING points outward, that is the side the player clicked
                    : DefaultBlockState
                        .SetValue(BlockStateProperties.AttachFaceProperty, AttachFace.wall)
                        .SetValue(BlockStateProperties.HorizontalFacing, face.ToState());
            return CanSurvive(level, pos, state) ? state : null;
        }

        //UpdateShape the whole block drops when the attached block is gone, maps to the vanilla method of the same name
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

        //GetConnectedDirection which face it attaches by, maps to vanilla getConnectedDirection
        public static Direction GetConnectedDirection(BlockState state)
            => state.GetValue(BlockStateProperties.AttachFaceProperty) switch
            {
                AttachFace.ceiling => Direction.Down,
                AttachFace.floor => Direction.Up,
                _ => state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive(),
            };

        //RotateAttachFace expands a north-facing shape into the three attach faces times the four horizontal directions, maps to vanilla Shapes.rotateAttachFace
        //AttachFace is in the Registry layer and Shapes in the Primitives layer, so the rotation lands on the Game-layer face-attached base class
        protected static Dictionary<AttachFace, Dictionary<Direction, VoxelShape>> RotateAttachFace(VoxelShape north)
            => new()
            {
                [AttachFace.wall] = Shapes.RotateHorizontal(north),
                [AttachFace.floor] = Shapes.RotateHorizontal(north, OctahedralGroups.BlockRotX270),
                [AttachFace.ceiling] = Shapes.RotateHorizontal(north,
                    OctahedralGroups.BlockRotY180.Compose(OctahedralGroups.BlockRotX90)),
            };

        //UpdateNeighbours notifies this block and the attached cell once each, maps to vanilla updateNeighbours
        protected void UpdateNeighbours(ServerLevel level, BlockPos pos, BlockState state)
        {
            var front = GetConnectedDirection(state).Opposite;
            level.UpdateNeighborsAt(pos, this);
            level.UpdateNeighborsAt(pos.Offset(front), this);
        }
    }

    //LeverBlock lever, right click toggles powered, maps to vanilla LeverBlock
    public sealed class LeverBlock : FaceAttachedHorizontalDirectionalBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("lever");

        //Lever shape, north is a slim block along Z from 10 to 16, maps to vanilla makeShapes
        //Without it the shape falls back to a full block, sky light is blocked and the lever's cell is darker than vanilla
        private static readonly Dictionary<AttachFace, Dictionary<NetCraft.Primitives.Direction, VoxelShape>>
            AttachShapes = RotateAttachFace(NetCraft.Registry.Block.BoxZ(6.0, 8.0, 10.0, 16.0));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => AttachShapes[state.GetValue(BlockStateProperties.AttachFaceProperty)]
                [state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];

        //Vanilla lever hardness 0.5, mineable bare-handed
        public override float DestroySpeed => 0.5f;

        //UseOn the lever takes no item, right click toggles directly, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            Pull(level, pos, state);
            return true;
        }

        //Pull toggles the powered state and notifies neighbors, maps to vanilla pull
        //The vanilla overload also takes player and gameEvent; the former only affects the sound recipient and the latter mechanism is not present here
        public void Pull(ServerLevel level, BlockPos pos, BlockState state)
        {
            var newState = state.Cycle(BlockStateProperties.Powered);
            level.SetBlock(pos, newState, BlockUpdateFlags.All);
            UpdateNeighbours(level, pos, newState);
            //Vanilla flipping has a click, pitch 0.6 when powered and 0.5 when off
            level.PlaySound(SoundEvents.LeverClick, SoundSource.Blocks, pos, 0.3f,
                newState.GetValue(BlockStateProperties.Powered) ? 0.6f : 0.5f);
        }

        //AffectNeighborsAfterRemoval a powered lever being removed must make neighbors recompute, maps to the vanilla method of the same name
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston && state.GetValue(BlockStateProperties.Powered))
                UpdateNeighbours(level, pos, state);
        }
    }

    //ButtonBlock button, right click presses and it pops back automatically, maps to vanilla ButtonBlock
    //ticksToStayPressed 20 ticks for stone and 30 for wood, given at registration
    public sealed class ButtonBlock : FaceAttachedHorizontalDirectionalBlock
    {
        private readonly string _name;
        private readonly int _ticksToStayPressed;

        public ButtonBlock(string name, int ticksToStayPressed)
        {
            _name = name;
            _ticksToStayPressed = ticksToStayPressed;
        }

        //Face-attached base plate, north is a thin block along Z from center to 16, maps to attachFace in vanilla makeShapes
        private static readonly Dictionary<AttachFace, Dictionary<NetCraft.Primitives.Direction, VoxelShape>>
            AttachShapes = RotateAttachFace(NetCraft.Registry.Block.BoxZ(6.0, 4.0, 8.0, 16.0));
        //The button body is 14 pixels when pressed and 12 when not, maps to vanilla pressedShaper/unpressedShaper
        private static readonly VoxelShape PressedCube = NetCraft.Registry.Block.Cube(14.0);
        private static readonly VoxelShape UnpressedCube = NetCraft.Registry.Block.Cube(12.0);

        //GetShape takes the plate part outside the union of the plate and the button body, maps to vanilla Shapes.join(..., ONLY_FIRST)
        //Without it the shape falls back to a full block, sky light is blocked and the button's cell is darker than vanilla
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
        {
            var support = AttachShapes[state.GetValue(BlockStateProperties.AttachFaceProperty)]
                [state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];
            var core = state.GetValue(BlockStateProperties.Powered) ? PressedCube : UnpressedCube;
            return Shapes.Join(support, core, BooleanOps.OnlyFirst);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //Vanilla button hardness 0.5, mineable bare-handed
        public override float DestroySpeed => 0.5f;

        //UseOn does not re-trigger when already pressed; vanilla returns CONSUME here, which also blocks placement
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) Press(level, pos, state);
            return true;
        }

        //Press presses and schedules a tick to pop back, maps to vanilla press
        public void Press(ServerLevel level, BlockPos pos, BlockState state)
        {
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, true),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            UpdateNeighbours(level, pos, state);
            level.ScheduleTick(pos, this, _ticksToStayPressed);
        }

        //Tick re-evaluates whether it is still pressed when the tick fires, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Powered)) CheckPressed(level, pos, state);
        }

        //OnEntityInside re-evaluates immediately when an entity touches it, maps to vanilla entityInside
        public override void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) CheckPressed(level, pos, state);
        }

        //AffectNeighborsAfterRemoval a pressed button being removed must make neighbors recompute
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston && state.GetValue(BlockStateProperties.Powered))
                UpdateNeighbours(level, pos, state);
        }

        //CheckPressed re-evaluates the pressed state, maps to vanilla checkPressed
        //Vanilla looks for arrows within the collision shape range here; this project has no arrow entity or shape system, so it is equivalent to never having an arrow
        //So the branch where only stone buttons are activated by arrows cannot fire for now; wooden buttons only respond to right click anyway
        private void CheckPressed(ServerLevel level, BlockPos pos, BlockState state)
        {
            if (!state.GetValue(BlockStateProperties.Powered)) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, false),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
            UpdateNeighbours(level, pos, state);
        }
    }

    //RedstoneTorchBlock redstone torch, stands on the ground, goes dark when there is a signal below and burns out from too much flickering
    public class RedstoneTorchBlock : BlockBehaviour
    {
        //REDSTONE_TORCH_BURNOUT world event id for burnout, maps to vanilla LevelEvent.REDSTONE_TORCH_BURNOUT
        public const int BurnoutEvent = 1502;
        //RecentToggleWindow counting window of 60 ticks, maps to vanilla RECENT_TOGGLE_TIMER
        public const int RecentToggleWindow = 60;
        //MaxRecentToggles burning out when toggles in the window reach this, maps to vanilla MAX_RECENT_TOGGLES
        public const int MaxRecentToggles = 8;
        //RestartDelay delay before relighting after a burnout, maps to vanilla RESTART_DELAY
        public const int RestartDelay = 160;
        //ToggleDelay delay from a neighbor change to re-evaluation, maps to vanilla TOGGLE_DELAY
        public const int ToggleDelay = 2;

        //_recentToggles recent toggle records, weakly attached per level so they are collected with the level, maps to vanilla RECENT_TOGGLES
        private static readonly ConditionalWeakTable<ServerLevel, List<ToggleEntry>> RecentToggles = new();

        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_torch");

        //Torch shape, 4 pixels wide and 10 high, maps to vanilla BaseTorchBlock.SHAPE
        //Without it the shape falls back to a full block, sky light is blocked and the torch's cell is darker than vanilla
        private static readonly VoxelShape TorchShape = NetCraft.Registry.Block.Column(4.0, 0.0, 10.0);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => TorchShape;

        //Vanilla torch hardness 0, breaks on touch
        public override float DestroySpeed => 0f;
        public override int LightEmission => 7;

        //A torch is not a conductor, signals travel via the direct signal path
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["lit"] = BlockStateProperties.Lit };

        //The vanilla torch is lit by default
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0].SetValue(BlockStateProperties.Lit, true);

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Lit) ? 15 : 0;

        //GetSignal a torch does not power upward and gives its own strength in other directions, maps to vanilla getSignal
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => direction == Direction.Up ? 0 : OwnSignal(level, pos, state);

        //GetDirectSignal only the downward direction counts as a direct signal, maps to vanilla getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => direction == Direction.Down ? GetSignal(level, pos, state, direction) : 0;

        //OnPlace notifies all six directions on placement, maps to vanilla onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston) => NotifyNeighbors(level, pos);

        //AffectNeighborsAfterRemoval also notifies on removal, only when not pushed by a piston, maps to the vanilla method of the same name
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston) NotifyNeighbors(level, pos);
        }

        //NeighborChanged schedules a re-evaluation tick when the lit state disagrees with the signal below, maps to vanilla neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            if (state.GetValue(BlockStateProperties.Lit) == HasNeighborSignal(level, pos, state)
                && !level.WillTickThisTick(pos, this))
                level.ScheduleTick(pos, this, ToggleDelay);
        }

        //Tick goes dark when lit with a signal below and lights when dark without, burns out from flickering too fast, maps to vanilla tick
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
                //A burnout only fires the event and extends the re-evaluation; the block itself stays unlit
                level.LevelEvent(BurnoutEvent, pos, 0);
                level.ScheduleTick(pos, this, RestartDelay);
                return;
            }

            if (!neighborSignal && !IsToggledTooFrequently(level, pos, false))
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Lit, true),
                    BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //HasNeighborSignal only checks the signal on the cell below, maps to the vanilla method of the same name
        protected virtual bool HasNeighborSignal(ServerLevel level, BlockPos pos, BlockState state)
            => level.HasSignal(pos.Offset(Direction.Down), Direction.Down);

        //UpdateShape drops when the support below is gone, maps to vanilla BaseTorchBlock.updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (directionToNeighbour == Direction.Down && !CanSurvive(level, pos, state))
                return AIR.DefaultBlockState;
            return state;
        }

        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
            => IsSupportSturdy(level, pos.Offset(Direction.Down), Direction.Up);

        //NotifyNeighbors notifies each of the six directions once, maps to vanilla notifyNeighbors
        private void NotifyNeighbors(ServerLevel level, BlockPos pos)
        {
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //IsToggledTooFrequently whether toggles at the same position within the window exceed the limit, add true records one first, maps to the vanilla method of the same name
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

        //IsSupportSturdy whether the support face is sturdy enough; without a shape system a full solid block is used as an approximation
        protected static bool IsSupportSturdy(ServerLevel level, BlockPos supportPos, Direction directionToSupport)
        {
            var support = level.GetBlockState(supportPos);
            return support is not null && support.Value.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, supportPos, support.Value, directionToSupport);
        }

        //ToggleEntry one toggle record, maps to vanilla RedstoneTorchBlock.Toggle
        private readonly record struct ToggleEntry(BlockPos Pos, long When);
    }

    //RedstoneWallTorchBlock wall redstone torch, the lit check switches to the attached side
    public sealed class RedstoneWallTorchBlock : RedstoneTorchBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_wall_torch");

        //Wall torch shapes are picked by facing, maps to vanilla WallTorchBlock.SHAPES
        private static readonly Dictionary<NetCraft.Primitives.Direction, VoxelShape> WallShapes =
            Shapes.RotateHorizontal(NetCraft.Registry.Block.BoxZ(5.0, 3.0, 13.0, 11.0, 16.0));

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => WallShapes[state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive()];

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["lit"] = BlockStateProperties.Lit,
        };

        //The vanilla wall torch default state is FACING=north LIT=true
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Lit, true);

        //Clicking a side places this block and the facing is the clicked side, maps to vanilla WallTorchBlock.getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
        {
            if (!face.IsHorizontal) return null;
            var state = DefaultBlockState.SetValue(BlockStateProperties.HorizontalFacing, face.ToState());
            return CanSurvive(level, pos, state) ? state : null;
        }

        //HasNeighborSignal checks the signal outside the attached face, maps to the vanilla override of the same name
        protected override bool HasNeighborSignal(ServerLevel level, BlockPos pos, BlockState state)
        {
            var back = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive().Opposite;
            return level.HasSignal(pos.Offset(back), back);
        }

        //GetSignal does not output toward the attached face, maps to the vanilla override of the same name
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction
                ? 0
                : OwnSignal(level, pos, state);

        //UpdateShape drops when the wall it is attached to is gone, maps to the vanilla override of the same name
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

    //BasePressurePlateBlock pressure plate base class, maps to vanilla BasePressurePlateBlock
    //Recomputes every PressedTime ticks once pressed, and on entity entry only recomputes immediately when not pressed
    public abstract class BasePressurePlateBlock : BlockBehaviour
    {
        //PressedTime re-evaluation interval once pressed, 20 ticks for simple plates and 10 for weighted
        protected virtual int PressedTime => 20;

        protected abstract int GetSignalForState(BlockState state);

        protected abstract BlockState SetSignalForState(BlockState state, int signal);

        protected abstract int GetSignalStrength(ServerLevel level, BlockPos pos);

        //Shape, 1 pixel thick unpressed and 0.5 pressed, maps to vanilla SHAPE/SHAPE_PRESSED
        //Without it the shape falls back to a full block, sky light is blocked and the plate's cell is darker than vanilla
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 1.0);
        private static readonly VoxelShape ShapePressed = NetCraft.Registry.Block.Column(14.0, 0.0, 0.5);

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => GetSignalForState(state) > 0 ? ShapePressed : Shape;

        //TouchBox entity detection box, same as vanilla TOUCH_AABB, one block wide inset from the base and 4 pixels tall
        protected static AABB TouchBox(BlockPos pos) => new(
            pos.X + 1 / 16.0, pos.Y, pos.Z + 1 / 16.0,
            pos.X + 15 / 16.0, pos.Y + 4 / 16.0, pos.Z + 15 / 16.0);

        //A pressure plate is not a conductor, signals only go upward
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsSignalSource => true;

        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => GetSignalForState(state);

        //GetDirectSignal only gives upward, maps to vanilla getDirectSignal
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => direction == Direction.Up ? GetSignalForState(state) : 0;

        //UpdateShape drops when the support below is gone, maps to the vanilla method of the same name
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

        //Tick recomputes while pressed, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            var signal = GetSignalForState(state);
            if (signal > 0) CheckPressed(level, pos, state, signal);
        }

        //OnEntityInside recomputes when an entity steps on it, no recompute needed when already pressed, maps to vanilla entityInside
        public override void OnEntityInside(ServerLevel level, BlockPos pos, BlockState state)
        {
            var signal = GetSignalForState(state);
            if (signal == 0) CheckPressed(level, pos, state, signal);
        }

        //AffectNeighborsAfterRemoval a pressed plate being removed must make neighbors recompute
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston && GetSignalForState(state) > 0) UpdateNeighbours(level, pos);
        }

        //CheckPressed recomputes the signal, writes it back and notifies neighbors on change and reschedules while still pressed, maps to vanilla checkPressed
        //The write-back uses only the Clients flag and neighbors are notified manually by updateNeighbours, same as vanilla flags 2
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

        //UpdateNeighbours notifies this block and the cell below once each, maps to vanilla updateNeighbours
        private void UpdateNeighbours(ServerLevel level, BlockPos pos)
        {
            level.UpdateNeighborsAt(pos, this);
            level.UpdateNeighborsAt(pos.Offset(Direction.Down), this);
        }
    }

    //PressurePlateBlock simple pressure plate, gives full output when stepped on, maps to vanilla PressurePlateBlock
    public sealed class PressurePlateBlock : BasePressurePlateBlock
    {
        private readonly string _name;

        public PressurePlateBlock(string name) => _name = name;

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //Vanilla pressure plate hardness 0.5, mineable bare-handed
        public override float DestroySpeed => 0.5f;

        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["powered"] = BlockStateProperties.Powered };

        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0].SetValue(BlockStateProperties.Powered, false);

        protected override int GetSignalForState(BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? 15 : 0;

        protected override BlockState SetSignalForState(BlockState state, int signal)
            => state.SetValue(BlockStateProperties.Powered, signal > 0);

        //GetSignalStrength gives full when there is an entity in the detection box, maps to vanilla counting after filtering by entity type
        //Vanilla stone and blackstone only count living entities; this project's entity types have no living flag yet, so all entities are counted for now
        protected override int GetSignalStrength(ServerLevel level, BlockPos pos)
            => level.CountEntitiesInBox(TouchBox(pos)) > 0 ? 15 : 0;
    }

    //WeightedPressurePlateBlock weighted pressure plate, the signal rises linearly with entity count, maps to the vanilla class of the same name
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

        //Weighted plates recompute every 10 ticks unlike the 20 of simple plates, maps to vanilla getPressedTime
        protected override int PressedTime => 10;

        public override IDictionary<string, PropertyBase> Properties
            => new Dictionary<string, PropertyBase> { ["power"] = BlockStateProperties.Power };

        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0].SetValue(BlockStateProperties.Power, 0);

        protected override int GetSignalForState(BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        protected override BlockState SetSignalForState(BlockState state, int signal)
            => state.SetValue(BlockStateProperties.Power, signal);

        //GetSignalStrength scales the entity count by the max weight and raises it to 0-15, maps to the vanilla method of the same name
        protected override int GetSignalStrength(ServerLevel level, BlockPos pos)
        {
            var count = Math.Min(level.CountEntitiesInBox(TouchBox(pos)), _maxWeight);
            return count <= 0 ? 0 : (int)MathF.Ceiling((float)count / _maxWeight * 15f);
        }
    }

    //DiodeBlock diode base class shared by repeaters and comparators, maps to vanilla DiodeBlock
    //Signals only output toward the FACING side and are 0 in other directions, the fundamental difference from a normal signal source
    //The input reads FACING forward and the side input reads the two clockwise and counterclockwise cells, together they decide the flip
    public abstract class DiodeBlock : BlockBehaviour
    {
        //The low plate shape is only 2 pixels high, maps to vanilla DiodeBlock.SHAPE
        //Without the override it falls back to a full block, the occlusion shape fills it and the repeater's cell loses a full 15 light levels, appearing black
        private static readonly VoxelShape LowShape = NetCraft.Registry.Block.Column(16.0, 0.0, 2.0);

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["powered"] = BlockStateProperties.Powered,
        };

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context) => LowShape;

        //GetDelay ticks from scheduling to the actual flip, repeaters follow their tier and comparators are always 2, maps to vanilla getDelay
        protected abstract int GetDelay(BlockState state);

        //A diode is not a conductor, redstone wire does not interconnect with it
        public override bool IsRedstoneConductor(ServerLevel level, BlockPos pos, BlockState state) => false;

        public override bool IsSignalSource => true;
        public override bool IsDiode => true;

        //The vanilla diode default state is FACING=north POWERED=false
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Powered, false);

        //GetStateForPlacement faces where the player looks, that is the opposite of the player's horizontal facing, maps to the vanilla method of the same name
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
            => DefaultBlockState.SetValue(BlockStateProperties.HorizontalFacing, horizontalFacing.Opposite.ToState());

        //CanSurvive only checks whether the cell below can support it, maps to vanilla canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            return CanSurviveOn(level, below, level.GetBlockState(below));
        }

        //CanSurviveOn whether the upward face of the neighboring block is sturdy enough, maps to vanilla canSurviveOn
        protected static bool CanSurviveOn(ServerLevel level, BlockPos neighbourPos, BlockState? neighbourState)
            => neighbourState?.Owner is BlockBehaviour behaviour
                && behaviour.IsFaceSturdy(level, neighbourPos, neighbourState.Value, Direction.Up);

        //UpdateShape the whole block drops when the support below is gone, maps to the respective overrides in vanilla repeaters and comparators
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
            => directionToNeighbour == Direction.Down && !CanSurviveOn(level, neighbourPos, neighbourState)
                ? AIR.DefaultBlockState
                : state;

        //IsLocked whether it is locked by a side input, locked diodes ignore input and do not flip; the base class never locks and repeaters override it
        public virtual bool IsLocked(ServerLevel level, BlockPos pos, BlockState state) => false;

        //Tick fires the scheduled tick and decides the flip from the current input, maps to vanilla tick
        //The write-back uses only the Clients flag and neighbors are covered by updateNeighboursInFront or the next neighbor change, same as vanilla flags 2
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
                //The input is gone by the time the tick fires, so the flip does not hold and another round is awaited, maps to this vanilla reschedule
                if (!shouldTurnOn) level.ScheduleTick(pos, this, GetDelay(state), TickPriority.VeryHigh);
            }
        }

        //NeighborChanged reschedules on an input-side or side change and drops when support is gone, maps to vanilla neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            //Vanilla first confirms the position still holds this block and does nothing if replaced
            if (!ReferenceEquals(level.GetBlockState(pos)?.Owner, this)) return;
            if (CanSurvive(level, pos, state))
            {
                CheckTickOnNeighbor(level, pos, state);
                return;
            }
            //Lost support goes through the full destroy path, drops and block entity contents are handled by the Game-layer destroy flow, maps to vanilla dropResources + removeBlock
            level.BlockUpdateSink?.DestroyBlock(pos, true, BlockUpdateFlags.UpdateLimitDefault);
            foreach (var direction in Direction.Values)
                level.UpdateNeighborsAt(pos.Offset(direction), this);
        }

        //CheckTickOnNeighbor schedules a tick when the current state disagrees with the expected one and none is scheduled yet, maps to vanilla checkTickOnNeighbor
        //Priority: highest when a reverse diode is in front, next when currently powered and normal otherwise
        protected virtual void CheckTickOnNeighbor(ServerLevel level, BlockPos pos, BlockState state)
        {
            var locked = IsLocked(level, pos, state);
            var on = state.GetValue(BlockStateProperties.Powered);
            var shouldTurnOn = ShouldTurnOn(level, pos, state);
            //The decision site is the core of redstone troubleshooting, both the input and side input are read to see whether the wrong signal was read
            Log.Debug($"Redstone diode check {pos} {state.Owner.Id}[{state.Id}] powered={on} should={shouldTurnOn} locked={locked} input={GetInputSignal(level, pos, state)} side={GetAlternateSignal(level, pos, state)} ticking={level.WillTickThisTick(pos, this)}");
            if (locked) return;
            if (on == shouldTurnOn || level.WillTickThisTick(pos, this)) return;
            var priority = TickPriority.High;
            if (ShouldPrioritize(level, pos, state)) priority = TickPriority.ExtremelyHigh;
            else if (on) priority = TickPriority.VeryHigh;
            level.ScheduleTick(pos, this, GetDelay(state), priority);
        }

        //ShouldTurnOn whether the input side has a signal, overridden by comparators, maps to the vanilla method of the same name
        protected virtual bool ShouldTurnOn(ServerLevel level, BlockPos pos, BlockState state)
            => GetInputSignal(level, pos, state) > 0;

        //GetInputSignal reads the signal of the cell FACING forward, maps to the vanilla method of the same name
        //When redstone wire is in front its own power is merged in; wire's direct signal to a diode is 0 so reading only getSignal would miss it
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

        //GetAlternateSignal takes the max of the clockwise and counterclockwise side inputs, maps to the vanilla method of the same name
        //The direction passed is from this block toward the neighbor, same as vanilla
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

        //SideInputDiodesOnly whether the side input only accepts diodes, true for repeaters and false for comparators, maps to the vanilla method of the same name
        protected virtual bool SideInputDiodesOnly() => false;

        //OwnSignal outputs its own output strength when powered and is always 0 when off, maps to vanilla ownSignal
        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Powered) ? GetOutputSignal(level, pos, state) : 0;

        //GetOutputSignal output strength, 15 by default for diodes and read from the block entity for comparators, maps to the vanilla method of the same name
        protected virtual int GetOutputSignal(ServerLevel level, BlockPos pos, BlockState state) => 15;

        //GetDirectSignal matches its own signal, maps to the vanilla override
        public override int GetDirectSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => GetSignal(level, pos, state, direction);

        //GetSignal only outputs toward the FACING side, maps to the vanilla override
        public override int GetSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() == direction
                ? OwnSignal(level, pos, state)
                : 0;

        //ShouldPrioritize lets a diode facing elsewhere in front jump the queue this tick, maps to the vanilla method of the same name
        //With two diodes back-to-back it ensures the one nearer the input computes first; a wrong order causes a missed beat or a deadlock
        public bool ShouldPrioritize(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive().Opposite;
            var oppositeState = level.GetBlockState(pos.Offset(direction));
            return oppositeState is { } opposite
                && opposite.Owner is IBlockSignalBehaviour { IsDiode: true }
                && opposite.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive() != direction;
        }

        //OnPlace notifies the input cell on placement, maps to vanilla onPlace
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston) => UpdateNeighborsInFront(level, pos, state);

        //AffectNeighborsAfterRemoval also notifies the input cell on removal, only when not pushed by a piston, maps to the vanilla method of the same name
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            if (!movedByPiston) UpdateNeighborsInFront(level, pos, state);
        }

        //SetPlacedBy schedules a tick when the input already has a signal at placement, maps to vanilla setPlacedBy
        //Without scheduling it would wait for the next neighbor change and the diode would stay dark if the input never moves
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
        {
            if (ShouldTurnOn(level, pos, state)) level.ScheduleTick(pos, this, 1);
        }

        //UpdateNeighborsInFront notifies the cell opposite FACING and its neighbors, maps to vanilla updateNeighborsInFront
        protected void UpdateNeighborsInFront(ServerLevel level, BlockPos pos, BlockState state)
        {
            var direction = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            var backPos = pos.Offset(direction.Opposite);
            level.NeighborChanged(backPos, this);
            level.UpdateNeighborsAtExceptFromFacing(backPos, this, direction);
        }
    }

    //RepeaterBlock redstone repeater, a one-way delay diode locked by a diode side input, maps to vanilla RepeaterBlock
    public sealed class RepeaterBlock : DiodeBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("repeater");

        //Vanilla repeater is instabreak with hardness 0, broken bare-handed instantly
        public override float DestroySpeed => 0f;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["delay"] = BlockStateProperties.Delay,
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["locked"] = BlockStateProperties.Locked,
            ["powered"] = BlockStateProperties.Powered,
        };

        //The vanilla repeater default state is FACING=north DELAY=1 LOCKED=false POWERED=false
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Delay, 1)
                .SetValue(BlockStateProperties.Locked, false)
                .SetValue(BlockStateProperties.Powered, false);

        //GetDelay two ticks per tier, maps to vanilla getDelay
        protected override int GetDelay(BlockState state) => state.GetValue(BlockStateProperties.Delay) * 2;

        //The side input only accepts diodes, wire connected to the side does not count, maps to vanilla sideInputDiodesOnly
        protected override bool SideInputDiodesOnly() => true;

        //IsLocked locks while a diode is powering from the side and does not flip while locked, maps to the vanilla override
        public override bool IsLocked(ServerLevel level, BlockPos pos, BlockState state)
            => GetAlternateSignal(level, pos, state) > 0;

        //UseOn right click cycles the delay tier, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            level.SetBlock(pos, state.Cycle(BlockStateProperties.Delay), BlockUpdateFlags.All);
            return true;
        }

        //GetStateForPlacement computes the lock once at placement, maps to the vanilla override
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
        {
            var state = base.GetStateForPlacement(level, pos, face, horizontalFacing);
            return state?.SetValue(BlockStateProperties.Locked, IsLocked(level, pos, state.Value));
        }

        //UpdateShape recomputes the lock when a side neighbor changes; the two cells on the facing axis do not affect it, maps to the vanilla override
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

    //ComparatorBlock redstone comparator, an analog diode; compare mode takes the input and subtract takes the input minus the side input, maps to vanilla ComparatorBlock
    public sealed class ComparatorBlock : DiodeBlock
    {
        public override Identifier Id => Identifier.WithDefaultNamespace("comparator");

        //Vanilla comparator is instabreak with hardness 0
        public override float DestroySpeed => 0f;

        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["mode"] = BlockStateProperties.ComparatorModeProperty,
            ["powered"] = BlockStateProperties.Powered,
        };

        //The vanilla comparator default state is FACING=north POWERED=false MODE=compare
        protected override BlockState CreateDefaultState()
            => StateDefinition.PossibleStates[0]
                .SetValue(BlockStateProperties.HorizontalFacing, StateDirection.north)
                .SetValue(BlockStateProperties.Powered, false)
                .SetValue(BlockStateProperties.ComparatorModeProperty, ComparatorMode.compare);

        //The comparator is always two ticks, maps to vanilla getDelay
        protected override int GetDelay(BlockState state) => 2;

        //CreateBlockEntity the comparator's output value is stored in the block entity, maps to vanilla newBlockEntity
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new ComparatorBlockEntity(pos);

        //HasBlockEntity the comparator has a block entity and cannot be pushed by a piston
        public override bool HasBlockEntity => true;

        //GetOutputSignal reads the last output stored in the block entity, maps to the vanilla override
        protected override int GetOutputSignal(ServerLevel level, BlockPos pos, BlockState state)
            => level.GetBlockEntity<ComparatorBlockEntity>(pos)?.OutputSignal ?? 0;

        //ShouldTurnOn turns on when the input exceeds the side input, and on equality only in compare mode, maps to the vanilla override
        protected override bool ShouldTurnOn(ServerLevel level, BlockPos pos, BlockState state)
        {
            var input = GetInputSignal(level, pos, state);
            if (input == 0) return false;
            var sideInput = GetAlternateSignal(level, pos, state);
            if (input > sideInput) return true;
            return input == sideInput
                && state.GetValue(BlockStateProperties.ComparatorModeProperty) == ComparatorMode.compare;
        }

        //GetInputSignal adds the analog value on top of the forward signal, maps to the vanilla override
        //If the block in front has an analog output it is used directly, otherwise the cell behind it is checked; vanilla also checks item frames at this level but this project has no item frame entity
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
            //Whether the cell behind a conductor gives an analog value; comparators use it to read containers like chests
            var behindPos = targetPos.Offset(direction);
            if (level.GetBlockState(behindPos) is not { } behind) return result;
            return behind.Owner is IBlockSignalBehaviour { HasAnalogOutputSignal: true } behindAnalog
                ? behindAnalog.GetAnalogOutputSignal(level, behindPos, behind, direction.Opposite)
                : result;
        }

        //CalculateOutputSignal computes the strength to output this time, maps to the vanilla method of the same name
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

        //CheckTickOnNeighbor schedules a tick only when the output value or powered state disagrees, maps to the vanilla override
        //The priority differs from repeaters: it is raised only when a reverse diode is in front and normal otherwise
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

        //RefreshOutputState writes back the output value and flips the powered state as needed, maps to the vanilla method of the same name
        //In compare mode it re-notifies regardless of whether the output changed; a side input change affects the comparison but not the output value
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

        //Tick comparators recompute the output directly when the tick fires and have no locking, maps to the vanilla override
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
            => RefreshOutputState(level, pos, state);

        //UseOn right click toggles compare/subtract, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (player.GameType.IsBlockPlacingRestricted) return false;
            var newState = state.Cycle(BlockStateProperties.ComparatorModeProperty);
            //Vanilla plays the comparator click sound here; block behaviors have no sound output, so it is deferred along with levers and buttons until the sound output opens up
            level.SetBlock(pos, newState, BlockUpdateFlags.Clients);
            if (ReferenceEquals(level.GetBlockState(pos)?.Owner, this))
                RefreshOutputState(level, pos, newState);
            return true;
        }
    }

    //RedstoneLampBlock redstone lamp, maps to vanilla RedstoneLampBlock
    //Lights immediately when powered and goes dark four ticks after losing power, emitting full light while lit
    public sealed class RedstoneLampBlock : BlockBehaviour
    {
        //LitDelay delay before going dark, maps to vanilla 4
        private const int LitDelay = 4;

        public override Identifier Id => Identifier.WithDefaultNamespace("redstone_lamp");

        //Vanilla redstone lamp hardness 0.3
        public override float DestroySpeed => 0.3f;

        //GetLightEmission emits light level 15 when lit, maps to vanilla lightLevel
        public override int GetLightEmission(BlockState state)
            => state.GetValue(BlockStateProperties.Lit) ? 15 : 0;

        //GetStateForPlacement decides the lit state from surrounding signals on placement, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState.SetValue(BlockStateProperties.Lit, level.HasNeighborSignal(pos));

        //NeighborChanged acts only when the signal disagrees with the lit state, maps to vanilla neighborChanged
        //Lighting is immediate and going dark is scheduled four ticks later, this is the vanilla rhythm; without it the lamp flickers as soon as it is powered
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var lit = state.GetValue(BlockStateProperties.Lit);
            if (lit == level.HasNeighborSignal(pos)) return;
            if (lit) level.ScheduleTick(pos, this, LitDelay);
            else level.SetBlock(pos, state.Cycle(BlockStateProperties.Lit), BlockUpdateFlags.Clients);
        }

        //Tick re-checks when the delay fires and goes dark only if there is still no signal, maps to vanilla tick
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

    //TargetBlock target block, maps to vanilla TargetBlock
    //When hit by a projectile the output strength comes from the hit point, stronger toward the center of the hit face, weakest 1 and strongest 15
    //Arrows hold for 20 ticks and other projectiles for 8; another hit during the hold does not overwrite the strength
    public sealed class TargetBlock : BlockBehaviour
    {
        //ActivationTicksArrows hold ticks after an arrow hit, maps to vanilla ACTIVATION_TICKS_ARROWS
        private const int ActivationTicksArrows = 20;

        //ActivationTicksOther hold ticks after other projectile hits, maps to vanilla ACTIVATION_TICKS_OTHER
        private const int ActivationTicksOther = 8;

        public override Identifier Id => Identifier.WithDefaultNamespace("target");

        public override bool IsSignalSource => true;

        //OwnSignal outputs the current strength, maps to vanilla ownSignal
        public override int OwnSignal(ServerLevel level, BlockPos pos, BlockState state)
            => state.GetValue(BlockStateProperties.Power);

        //OnProjectileHit writes the strength and schedules the hold tick on a hit, maps to vanilla onProjectileHit
        public override void OnProjectileHit(ServerLevel level, BlockState state, BlockHitResult hit,
            Projectile projectile)
        {
            var strength = GetRedstoneStrength(hit);
            var duration = projectile is AbstractArrow ? ActivationTicksArrows : ActivationTicksOther;
            //Still having a scheduled hold tick means the strength is already running, so it is not rewritten, maps to the vanilla hasScheduledTick check
            if (level.HasScheduledTick(hit.BlockPos, this)) return;
            SetOutputPower(level, state, hit.BlockPos, strength, duration);
        }

        //Tick zeroes the strength when the hold expires, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
        {
            if (state.GetValue(BlockStateProperties.Power) != 0)
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, 0), BlockUpdateFlags.All);
        }

        //OnPlace clears a leftover strength with no scheduled tick when a target is placed, maps to vanilla onPlace
        //The write-back carries KnownShape so the cleanup step does not trigger a shape update
        public override void OnPlace(ServerLevel level, BlockPos pos, BlockState state, BlockState oldState,
            bool movedByPiston)
        {
            if (ReferenceEquals(oldState.Owner, state.Owner)) return;
            if (state.GetValue(BlockStateProperties.Power) <= 0 || level.HasScheduledTick(pos, this)) return;
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, 0),
                BlockUpdateFlags.KnownShape | BlockUpdateFlags.Clients);
        }

        //SetOutputPower writes the strength and schedules the hold tick, maps to vanilla setOutputPower
        private void SetOutputPower(ServerLevel level, BlockState state, BlockPos pos, int strength, int duration)
        {
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Power, strength), BlockUpdateFlags.All);
            level.ScheduleTick(pos, this, duration);
        }

        //GetRedstoneStrength takes the larger of the two axes other than the hit face and maps it linearly to 1..15
        //Maps to vanilla getRedstoneStrength; a hit point at 0.5 in the cell gives offset 0 and full strength
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

    //Buttons: stone 20 ticks, wood and fungus 30, maps to ticksToStayPressed at the vanilla registration
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

    //Simple pressure plates give full output when stepped on
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

    //Weighted pressure plates: light golden max weight 15 and heavy iron 150, maps to the vanilla registration
    public static readonly WeightedPressurePlateBlock LIGHT_WEIGHTED_PRESSURE_PLATE =
        new("light_weighted_pressure_plate", 15);
    public static readonly WeightedPressurePlateBlock HEAVY_WEIGHTED_PRESSURE_PLATE =
        new("heavy_weighted_pressure_plate", 150);

    //RegisterRedstone registers redstone components into the real block table; there are many so they are registered by iterating fields with the registry name as the key
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
