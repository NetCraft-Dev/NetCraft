using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Storage.Updates;
using NetCraft.Game.World.Phys.Collision;
using Direction = NetCraft.Primitives.Direction;
using RegBlock = NetCraft.Registry.Block;

namespace NetCraft.Game.World.Level.Block;

//Fence gates, maps to vanilla net.minecraft.world.level.block.FenceGateBlock
//Opens outward along the facing, the panel is lowered when walled on both sides, redstone opens and closes by signal
//Vanilla open and close come with an animation; here there are only block states and sounds
public static partial class Blocks
{
    public static readonly FenceGateBlock OAK_FENCE_GATE = new("oak_fence_gate", BlockSet.Wood);
    public static readonly FenceGateBlock CRIMSON_FENCE_GATE =
        new("crimson_fence_gate", BlockSet.NetherWood);

    //RegisterFenceGates registers fence gates into the real block table, registry name to wood tier follows vanilla Blocks.java
    private static void RegisterFenceGates(Dictionary<string, BlockBehaviour> real)
    {
        RegisterFenceGate(real, OAK_FENCE_GATE);
        RegisterFenceGate(real, new FenceGateBlock("spruce_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("birch_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("jungle_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("acacia_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("dark_oak_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("pale_oak_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("mangrove_fence_gate", BlockSet.Wood));
        RegisterFenceGate(real, new FenceGateBlock("cherry_fence_gate", BlockSet.Cherry));
        RegisterFenceGate(real, new FenceGateBlock("bamboo_fence_gate", BlockSet.Bamboo));
        RegisterFenceGate(real, CRIMSON_FENCE_GATE);
        RegisterFenceGate(real, new FenceGateBlock("warped_fence_gate", BlockSet.NetherWood));
    }

    private static void RegisterFenceGate(Dictionary<string, BlockBehaviour> real, FenceGateBlock block)
        => real[block.Id.Path] = block;

    public sealed class FenceGateBlock : BlockBehaviour
    {
        //GateShapes gate panel shapes, sixteen by sixteen by four, maps to vanilla SHAPES
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateShapes =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Registry.Block.Cube(16.0, 16.0, 4.0));

        //GateShapesWall lowers the panel below thirteen when walled, maps to vanilla SHAPES_WALL
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateShapesWall =
            MapValues(GateShapes, shape => NetCraft.Primitives.Phys.Shapes.Join(shape,
                NetCraft.Registry.Block.Column(16.0, 13.0, 16.0), BooleanOps.OnlyFirst));

        //GateCollision collision box when closed, one and a half blocks tall, maps to vanilla SHAPE_COLLISION
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateCollision =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Registry.Block.Column(16.0, 4.0, 0.0, 24.0));

        //GateSupport support shape when closed, maps to vanilla SHAPE_SUPPORT
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateSupport =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Registry.Block.Column(16.0, 4.0, 5.0, 24.0));

        //GateOcclusion occlusion shape of the two posts, maps to vanilla SHAPE_OCCLUSION
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateOcclusion =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Primitives.Phys.Shapes.Or(
                NetCraft.Registry.Block.Box(0.0, 5.0, 7.0, 2.0, 16.0, 9.0),
                NetCraft.Registry.Block.Box(14.0, 5.0, 7.0, 16.0, 16.0, 9.0)));

        //GateOcclusionWall the occlusion shape shifts down three pixels when walled, maps to vanilla SHAPE_OCCLUSION_WALL
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateOcclusionWall =
            MapValues(GateOcclusion, shape => shape.Move(0.0, -0.1875, 0.0).Optimize());

        private readonly string _name;
        private readonly SoundEvent _openSound;
        private readonly SoundEvent _closeSound;

        public FenceGateBlock(string name, BlockSet wood)
        {
            _name = name;
            (_openSound, _closeSound) = SoundsOf(wood);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //Vanilla fence gate hardness 2, wood is mineable bare-handed
        public override float DestroySpeed => 2f;

        //Properties states follow facing|in_wall|open|powered in blocks.txt
        //The one in the table is a separately built property instance that GetValue cannot find; it must be declared here, and changing the order shifts the global state ids
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["in_wall"] = BlockStateProperties.InWall,
            ["open"] = BlockStateProperties.Open,
            ["powered"] = BlockStateProperties.Powered,
        };

        //GetShape uses the lowered table when walled, maps to vanilla getShape
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => (state.GetValue(BlockStateProperties.InWall) ? GateShapesWall : GateShapes)[AxisOf(state)];

        //GetCollisionShape does not block when open, maps to vanilla getCollisionShape
        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => state.GetValue(BlockStateProperties.Open) ? Shapes.Empty() : GateCollision[AxisOf(state)];

        //GetBlockSupportShape you can stand on it when closed, maps to vanilla getBlockSupportShape
        public override VoxelShape GetBlockSupportShape(BlockState state, BlockGetter level, BlockPos pos)
            => state.GetValue(BlockStateProperties.Open) ? Shapes.Empty() : GateSupport[AxisOf(state)];

        //GetOcclusionShape only the two posts occlude light, maps to vanilla getOcclusionShape
        public override VoxelShape GetOcclusionShape(BlockState state)
            => (state.GetValue(BlockStateProperties.InWall) ? GateOcclusionWall : GateOcclusion)[AxisOf(state)];

        //UpdateShape lowers the panel when there are walls on both sides of the axis perpendicular to the facing, maps to vanilla updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (AxisOf(directionToNeighbour) == AxisOf(state)) return state;
            var inWall = IsWall(neighbourState)
                || IsWall(level.GetBlockState(pos.Offset(directionToNeighbour.Opposite)));
            return state.SetValue(BlockStateProperties.InWall, inWall);
        }

        //GetStateForPlacement the facing is the player facing without inverting, walls on both sides place it directly in the walled state
        //An existing signal beside places it already open and powered, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing)
        {
            var isOpen = level.HasNeighborSignal(pos);
            var state = DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing, ToPropertyFacing(horizontalFacing))
                .SetValue(BlockStateProperties.Open, isOpen)
                .SetValue(BlockStateProperties.Powered, isOpen);
            return state.SetValue(BlockStateProperties.InWall, HasWallSides(level, pos, horizontalFacing));
        }

        //UseOn right click to open and close; opening a closed gate turns the facing around when it faces against the player, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (state.GetValue(BlockStateProperties.Open))
            {
                state = state.SetValue(BlockStateProperties.Open, false);
            }
            else
            {
                var facing = Direction.FromYRot(player.Yaw);
                if (state.GetValue(BlockStateProperties.HorizontalFacing) == ToPropertyFacing(facing.Opposite))
                    state = state.SetValue(BlockStateProperties.HorizontalFacing, ToPropertyFacing(facing));
                state = state.SetValue(BlockStateProperties.Open, true);
            }
            //Vanilla only syncs the client without notifying neighbors; this project has no late queue so the immediate flag is unnecessary
            level.SetBlock(pos, state, BlockUpdateFlags.Clients);
            PlaySound(level, pos, state.GetValue(BlockStateProperties.Open));
            return true;
        }

        //NeighborChanged opens and closes on a signal flip and records the powered state, maps to vanilla neighborChanged
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            RegBlock changedBlock, bool movedByPiston)
        {
            var signal = level.HasNeighborSignal(pos);
            if (state.GetValue(BlockStateProperties.Powered) == signal) return;
            var wasOpen = state.GetValue(BlockStateProperties.Open);
            level.SetBlock(pos, state.SetValue(BlockStateProperties.Powered, signal)
                .SetValue(BlockStateProperties.Open, signal), BlockUpdateFlags.Clients);
            if (wasOpen != signal) PlaySound(level, pos, signal);
        }

        //PlaySound open and close sound, the pitch jitters between 0.9 and 1.0, maps to playSound in vanilla useWithoutItem
        private void PlaySound(ServerLevel level, BlockPos pos, bool opening)
        {
            var sound = opening ? _openSound : _closeSound;
            level.PlaySound(sound, SoundSource.Blocks, pos, 1f, Random.Shared.NextSingle() * 0.1f + 0.9f);
        }

        //SoundsOf the open and close sounds for a wood tier, maps to fenceGateOpen/fenceGateClose of each vanilla WoodType tier
        private static (SoundEvent Open, SoundEvent Close) SoundsOf(BlockSet wood) => wood switch
        {
            BlockSet.Cherry =>
                (SoundEvents.CherryWoodFenceGateOpen, SoundEvents.CherryWoodFenceGateClose),
            BlockSet.Bamboo =>
                (SoundEvents.BambooWoodFenceGateOpen, SoundEvents.BambooWoodFenceGateClose),
            BlockSet.NetherWood =>
                (SoundEvents.NetherWoodFenceGateOpen, SoundEvents.NetherWoodFenceGateClose),
            _ => (SoundEvents.FenceGateOpen, SoundEvents.FenceGateClose),
        };

        //IsWall whether this state is a wall block, maps to vanilla isWall which checks the walls tag
        private static bool IsWall(BlockState? state)
            => state is { Owner: BlockBehaviour behaviour } && behaviour.IsInTag(BlockTags.Walls);

        //HasWallSides whether there are walls on both sides perpendicular to the facing, maps to the inWall check in vanilla getStateForPlacement
        private static bool HasWallSides(ServerLevel level, BlockPos pos, Direction facing)
        {
            if (facing == Direction.North || facing == Direction.South)
                return IsWall(level.GetBlockState(pos.Offset(Direction.West)))
                    || IsWall(level.GetBlockState(pos.Offset(Direction.East)));
            return IsWall(level.GetBlockState(pos.Offset(Direction.North)))
                || IsWall(level.GetBlockState(pos.Offset(Direction.South)));
        }

        //AxisOf which axis the horizontal facing lies on, north-south is Z and east-west is X
        private static Direction.Axis AxisOf(BlockState state)
            => state.GetValue(BlockStateProperties.HorizontalFacing) is NetCraft.Registry.Enums.Direction.north
                or NetCraft.Registry.Enums.Direction.south
                ? Direction.Axis.Z
                : Direction.Axis.X;

        private static Direction.Axis AxisOf(Direction direction)
            => direction == Direction.North || direction == Direction.South ? Direction.Axis.Z : Direction.Axis.X;

        //ToPropertyFacing converts a geometry direction into the enum member used by block properties, fence gates only use the four horizontal ones
        private static NetCraft.Registry.Enums.Direction ToPropertyFacing(Direction direction)
        {
            if (direction == Direction.North) return NetCraft.Registry.Enums.Direction.north;
            if (direction == Direction.South) return NetCraft.Registry.Enums.Direction.south;
            if (direction == Direction.West) return NetCraft.Registry.Enums.Direction.west;
            return NetCraft.Registry.Enums.Direction.east;
        }

        //MapValues replaces each shape in the axis table, maps to vanilla Util.mapValues
        private static Dictionary<Direction.Axis, VoxelShape> MapValues(
            Dictionary<Direction.Axis, VoxelShape> source, Func<VoxelShape, VoxelShape> transform)
        {
            var result = new Dictionary<Direction.Axis, VoxelShape>(source.Count);
            foreach (var (axis, shape) in source) result[axis] = transform(shape);
            return result;
        }
    }
}
