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

//栅栏门 对应原版 net.minecraft.world.level.block.FenceGateBlock
//朝向往外开 两侧夹在墙里时门板压低 红石按信号开合
//原版开合会附带动画 本作只有方块状态与音效
public static partial class Blocks
{
    public static readonly FenceGateBlock OAK_FENCE_GATE = new("oak_fence_gate", BlockSet.Wood);
    public static readonly FenceGateBlock CRIMSON_FENCE_GATE =
        new("crimson_fence_gate", BlockSet.NetherWood);

    //RegisterFenceGates 栅栏门登记进真实方块表 注册名到木料档照原版 Blocks.java
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
        //GateShapes 门板形状 十六格宽十六格高四格厚 对应原版 SHAPES
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateShapes =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Registry.Block.Cube(16.0, 16.0, 4.0));

        //GateShapesWall 夹在墙里时把门板压到十三格以下 对应原版 SHAPES_WALL
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateShapesWall =
            MapValues(GateShapes, shape => NetCraft.Primitives.Phys.Shapes.Join(shape,
                NetCraft.Registry.Block.Column(16.0, 13.0, 16.0), BooleanOps.OnlyFirst));

        //GateCollision 关闭时的碰撞箱 高出一格半 对应原版 SHAPE_COLLISION
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateCollision =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Registry.Block.Column(16.0, 4.0, 0.0, 24.0));

        //GateSupport 关闭时的依附形状 对应原版 SHAPE_SUPPORT
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateSupport =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Registry.Block.Column(16.0, 4.0, 5.0, 24.0));

        //GateOcclusion 两侧立柱的遮挡形状 对应原版 SHAPE_OCCLUSION
        private static readonly Dictionary<Direction.Axis, VoxelShape> GateOcclusion =
            NetCraft.Primitives.Phys.Shapes.RotateHorizontalAxis(NetCraft.Primitives.Phys.Shapes.Or(
                NetCraft.Registry.Block.Box(0.0, 5.0, 7.0, 2.0, 16.0, 9.0),
                NetCraft.Registry.Block.Box(14.0, 5.0, 7.0, 16.0, 16.0, 9.0)));

        //GateOcclusionWall 夹在墙里时遮挡形状整体下移三像素 对应原版 SHAPE_OCCLUSION_WALL
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

        //原版栅栏门硬度 2 木质空手可挖
        public override float DestroySpeed => 2f;

        //Properties 状态按 blocks.txt 的 facing|in_wall|open|powered 走
        //表里那份是另建的属性实例 GetValue 取不到 必须自己声明 顺序变了全局状态 id 会错位
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["in_wall"] = BlockStateProperties.InWall,
            ["open"] = BlockStateProperties.Open,
            ["powered"] = BlockStateProperties.Powered,
        };

        //GetShape 夹在墙里时用压低的那张表 对应原版 getShape
        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => (state.GetValue(BlockStateProperties.InWall) ? GateShapesWall : GateShapes)[AxisOf(state)];

        //GetCollisionShape 开着时不挡路 对应原版 getCollisionShape
        public override VoxelShape GetCollisionShape(BlockState state, BlockGetter level, BlockPos pos,
            CollisionContext context)
            => state.GetValue(BlockStateProperties.Open) ? Shapes.Empty() : GateCollision[AxisOf(state)];

        //GetBlockSupportShape 关闭时上面能站人 对应原版 getBlockSupportShape
        public override VoxelShape GetBlockSupportShape(BlockState state, BlockGetter level, BlockPos pos)
            => state.GetValue(BlockStateProperties.Open) ? Shapes.Empty() : GateSupport[AxisOf(state)];

        //GetOcclusionShape 只有两侧立柱挡光 对应原版 getOcclusionShape
        public override VoxelShape GetOcclusionShape(BlockState state)
            => (state.GetValue(BlockStateProperties.InWall) ? GateOcclusionWall : GateOcclusion)[AxisOf(state)];

        //UpdateShape 垂直于朝向的那条轴上两侧有墙就把门板压下去 对应原版 updateShape
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (AxisOf(directionToNeighbour) == AxisOf(state)) return state;
            var inWall = IsWall(neighbourState)
                || IsWall(level.GetBlockState(pos.Offset(directionToNeighbour.Opposite)));
            return state.SetValue(BlockStateProperties.InWall, inWall);
        }

        //GetStateForPlacement 朝向取玩家朝向不取反 两侧有墙则直接落成夹墙态
        //旁边已有信号时落成打开且通电 对应原版 getStateForPlacement
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

        //UseOn 右键开合 关着开的时候朝向与玩家相反就顺手转过来 对应原版 useWithoutItem
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
            //原版只同步客户端不通知邻居 本作没有迟到队列 立即生效这一位不需要
            level.SetBlock(pos, state, BlockUpdateFlags.Clients);
            PlaySound(level, pos, state.GetValue(BlockStateProperties.Open));
            return true;
        }

        //NeighborChanged 信号翻转时开合并记通电态 对应原版 neighborChanged
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

        //PlaySound 开合音效 音高在 0.9 到 1.0 之间抖动 对应原版 useWithoutItem 里的 playSound
        private void PlaySound(ServerLevel level, BlockPos pos, bool opening)
        {
            var sound = opening ? _openSound : _closeSound;
            level.PlaySound(sound, SoundSource.Blocks, pos, 1f, Random.Shared.NextSingle() * 0.1f + 0.9f);
        }

        //SoundsOf 木料档对应的开合音效 对应原版 WoodType 各档的 fenceGateOpen/fenceGateClose
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

        //IsWall 该状态是不是墙类方块 对应原版 isWall 走 walls 标签判定
        private static bool IsWall(BlockState? state)
            => state is { Owner: BlockBehaviour behaviour } && behaviour.IsInTag(BlockTags.Walls);

        //HasWallSides 垂直于朝向的两侧有没有墙 对应原版 getStateForPlacement 里的 inWall 判定
        private static bool HasWallSides(ServerLevel level, BlockPos pos, Direction facing)
        {
            if (facing == Direction.North || facing == Direction.South)
                return IsWall(level.GetBlockState(pos.Offset(Direction.West)))
                    || IsWall(level.GetBlockState(pos.Offset(Direction.East)));
            return IsWall(level.GetBlockState(pos.Offset(Direction.North)))
                || IsWall(level.GetBlockState(pos.Offset(Direction.South)));
        }

        //AxisOf 水平朝向落在哪条轴上 南北是 Z 东西是 X
        private static Direction.Axis AxisOf(BlockState state)
            => state.GetValue(BlockStateProperties.HorizontalFacing) is NetCraft.Registry.Enums.Direction.north
                or NetCraft.Registry.Enums.Direction.south
                ? Direction.Axis.Z
                : Direction.Axis.X;

        private static Direction.Axis AxisOf(Direction direction)
            => direction == Direction.North || direction == Direction.South ? Direction.Axis.Z : Direction.Axis.X;

        //ToPropertyFacing 几何方向折成方块属性用的枚举成员 栅栏门只会用到水平四个
        private static NetCraft.Registry.Enums.Direction ToPropertyFacing(Direction direction)
        {
            if (direction == Direction.North) return NetCraft.Registry.Enums.Direction.north;
            if (direction == Direction.South) return NetCraft.Registry.Enums.Direction.south;
            if (direction == Direction.West) return NetCraft.Registry.Enums.Direction.west;
            return NetCraft.Registry.Enums.Direction.east;
        }

        //MapValues 把轴表里的形状逐个换一遍 对应原版 Util.mapValues
        private static Dictionary<Direction.Axis, VoxelShape> MapValues(
            Dictionary<Direction.Axis, VoxelShape> source, Func<VoxelShape, VoxelShape> transform)
        {
            var result = new Dictionary<Direction.Axis, VoxelShape>(source.Count);
            foreach (var (axis, shape) in source) result[axis] = transform(shape);
            return result;
        }
    }
}
