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

//门 对应原版 net.minecraft.world.level.block.DoorBlock
//占上下两格 合页在左还是在右由落位时的两侧墙与点击位置决定 开合按玩家朝向与合页转向
//铁门不能徒手开 红石只看自己与另一半那一格有没有信号
public static partial class Blocks
{
    public static readonly DoorBlock OAK_DOOR = new("oak_door", BlockSet.Wood);
    public static readonly DoorBlock IRON_DOOR = new("iron_door", BlockSet.Iron);
    public static readonly DoorBlock COPPER_DOOR = new("copper_door", BlockSet.Copper);

    //RegisterDoors 门登记进真实方块表 注册名到材质档照原版 Blocks.java
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
        //DoorShapes 门板形状 十六格宽十六格高三格厚 对应原版 SHAPES
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
            //铁门不能徒手开 其余材质都可以 对应原版 BlockSetType.canOpenByHand
            _canOpenByHand = material != BlockSet.Iron;
            (_openSound, _closeSound) = SoundsOf(material);
        }

        public override Identifier Id => Identifier.WithDefaultNamespace(_name);

        //木质门硬度 3 铁门 5 铜门 3 对应原版各档 strength
        public override float DestroySpeed => _material == BlockSet.Iron ? 5f : 3f;

        //木质空手可挖 铁与铜要正确工具
        public override bool RequiresCorrectToolForDrops
            => _material is BlockSet.Iron or BlockSet.Copper;

        //Properties 状态按 blocks.txt 的 facing|half|hinge|open|powered 走
        //表里那份是另建的属性实例 GetValue 取不到 必须自己声明 顺序变了全局状态 id 会错位
        public override IDictionary<string, PropertyBase> Properties => new Dictionary<string, PropertyBase>
        {
            ["facing"] = BlockStateProperties.HorizontalFacing,
            ["half"] = BlockStateProperties.DoubleBlockHalfProperty,
            ["hinge"] = BlockStateProperties.DoorHinge,
            ["open"] = BlockStateProperties.Open,
            ["powered"] = BlockStateProperties.Powered,
        };

        //GetShape 关着贴朝向那面 开着按合页转到朝向的左右一侧 对应原版 getShape
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

        //GetStateForPlacement 只给下半 上方能被替换才放得下 合页侧由两侧与点击位置定
        //旁边自己或上方有信号就直接落成打开且通电 对应原版 getStateForPlacement
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

        //SetPlacedBy 放完下半顺手在上面补上半 对应原版 setPlacedBy
        public override void SetPlacedBy(ServerLevel level, BlockPos pos, BlockState state, ServerPlayer player)
            => level.SetBlock(pos.Offset(Direction.Up),
                state.SetValue(BlockStateProperties.DoubleBlockHalfProperty, DoubleBlockHalf.upper),
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);

        //UpdateShape 上下那格邻居变了要跟另一半对齐 半截门自己消失 对应原版 updateShape
        //下半下方没支撑也消失
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            var half = state.GetValue(BlockStateProperties.DoubleBlockHalfProperty);
            var neighbourAbove = directionToNeighbour == Direction.Up;
            if ((directionToNeighbour == Direction.Up || directionToNeighbour == Direction.Down)
                && (half == DoubleBlockHalf.lower) == neighbourAbove)
            {
                //另一半是门的另一截就跟着它对齐 否则本截没有存在的意义
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

        //CanSurvive 下半要下方能顶住 上半要下方是自己 对应原版 canSurvive
        public override bool CanSurvive(ServerLevel level, BlockPos pos, BlockState state)
        {
            var below = pos.Offset(Direction.Down);
            var belowState = level.GetBlockState(below);
            if (state.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower)
                return belowState is { Owner: BlockBehaviour behaviour }
                    && behaviour.IsFaceSturdy(level, below, belowState.Value, Direction.Up);
            return belowState is { } lower && ReferenceEquals(lower.Owner, this);
        }

        //AffectNeighborsAfterRemoval 自己没了就把另一半无掉落抹掉 对应原版双格方块的掉落抑制
        //不抹的话剩下的那半会跟着形状更新销毁再掉一份 整扇门一次掉两个(Mojira MC-188675)
        //原版把这件事放在 playerWillDestroy 里 只覆盖玩家破坏 这里改挂移除钩子
        //玩家破坏与支撑丢失两条路径都能覆盖 结果都是整扇门只掉一份
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
        {
            var half = state.GetValue(BlockStateProperties.DoubleBlockHalfProperty);
            var otherPos = half == DoubleBlockHalf.lower ? pos.Offset(Direction.Up) : pos.Offset(Direction.Down);
            //另一半得是同一扇门的另一截 已经不是门就直接收手 顺带断掉两边互相抹的递归
            if (level.GetBlockState(otherPos) is not { } other
                || !ReferenceEquals(other.Owner, this)
                || other.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == half)
                return;
            //只写状态不产生掉落 原版 preventDropFromBottomPart 也是用 setBlock 直接替换成空气
            level.SetBlock(otherPos, AIR.DefaultBlockState,
                BlockUpdateFlags.Neighbours | BlockUpdateFlags.Clients);
        }

        //UseOn 徒手开合 打不开的材质直接不受理 对应原版 useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state,
            Direction face)
        {
            if (!_canOpenByHand) return false;
            var updated = state.Cycle(BlockStateProperties.Open);
            //原版只同步客户端不通知邻居 形状更新会把新状态带给另一半让它一起转
            level.SetBlock(pos, updated, BlockUpdateFlags.Clients);
            PlaySound(level, pos, updated.GetValue(BlockStateProperties.Open));
            return true;
        }

        //NeighborChanged 自己或另一半那格有信号就开合并记通电 对应原版 neighborChanged
        //变化的就是门自己时不动 免得开门触发自己再跑一遍
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

        //PlaySound 开合音效 音高在 0.9 到 1.0 之间抖动 对应原版 playSound
        private void PlaySound(ServerLevel level, BlockPos pos, bool opening)
        {
            var sound = opening ? _openSound : _closeSound;
            level.PlaySound(sound, SoundSource.Blocks, pos, 1f, Random.Shared.NextSingle() * 0.1f + 0.9f);
        }

        //GetHinge 合页侧判定 先看左右两列有没有挡的方块与已有的门下截 都看不出才按点击点在门的哪半边
        //对应原版 getHinge
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

        //IsFullBlock 该状态的碰撞形状是否占满整格 对应原版 isCollisionShapeFullBlock
        //形状只看方块自身 世界视图给空的即可
        private static bool IsFullBlock(BlockState? state, BlockPos pos)
            => state is { Owner: BlockBehaviour behaviour }
                && behaviour.IsCollisionShapeFullBlock(state.Value, EmptyBlockGetter.Instance, pos);

        //IsLowerDoor 该状态是不是别人的门下截 对应原版 getHinge 里的 doorLeft/doorRight
        private static bool IsLowerDoor(BlockState? state)
            => state is { Owner: DoorBlock }
                && state.Value.GetValue(BlockStateProperties.DoubleBlockHalfProperty) == DoubleBlockHalf.lower;

        //SoundsOf 材质档对应的开合音效 对应原版 BlockSetType 各档的 doorOpen/doorClose
        private static (SoundEvent Open, SoundEvent Close) SoundsOf(BlockSet material) => material switch
        {
            BlockSet.Iron => (SoundEvents.IronDoorOpen, SoundEvents.IronDoorClose),
            BlockSet.Copper => (SoundEvents.CopperDoorOpen, SoundEvents.CopperDoorClose),
            BlockSet.Cherry => (SoundEvents.CherryWoodDoorOpen, SoundEvents.CherryWoodDoorClose),
            BlockSet.Bamboo => (SoundEvents.BambooWoodDoorOpen, SoundEvents.BambooWoodDoorClose),
            BlockSet.NetherWood => (SoundEvents.NetherWoodDoorOpen, SoundEvents.NetherWoodDoorClose),
            _ => (SoundEvents.WoodenDoorOpen, SoundEvents.WoodenDoorClose),
        };

        //ToPropertyFacing 几何方向折成方块属性用的枚举成员 门只会用到水平四个
        private static NetCraft.Registry.Enums.Direction ToPropertyFacing(Direction direction)
        {
            if (direction == Direction.North) return NetCraft.Registry.Enums.Direction.north;
            if (direction == Direction.South) return NetCraft.Registry.Enums.Direction.south;
            if (direction == Direction.West) return NetCraft.Registry.Enums.Direction.west;
            return NetCraft.Registry.Enums.Direction.east;
        }

        //ToGeometry 属性枚举折回几何方向 只用来查形状表与转向
        private static Direction ToGeometry(NetCraft.Registry.Enums.Direction direction) => direction switch
        {
            NetCraft.Registry.Enums.Direction.north => Direction.North,
            NetCraft.Registry.Enums.Direction.south => Direction.South,
            NetCraft.Registry.Enums.Direction.west => Direction.West,
            _ => Direction.East,
        };
    }
}
