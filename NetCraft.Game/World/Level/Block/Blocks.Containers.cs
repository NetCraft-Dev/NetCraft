using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
//属性枚举里的 Direction 与 Primitives.Direction 同名 只取需要的箱子形态
using ChestType = NetCraft.Registry.Enums.ChestType;

namespace NetCraft.Game.World.Level.Block;

//V-8 容器类方块 打开界面走 MenuProvider 服务端下发 open_screen 客户端按菜单类型开屏
public static partial class Blocks
{
    public static readonly ChestBlock CHEST = new("chest");
    public static readonly BarrelBlock BARREL = new("barrel");

    //RegisterContainers 容器类方块登记进真实方块表
    private static void RegisterContainers(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { CHEST, BARREL };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //ChestBlock 箱子对应原版 net.minecraft.world.level.block.ChestBlock
    //同朝向的两格箱子自动连成双箱 目标容器由两半合成 菜单六个行
    public sealed class ChestBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 14.0);

        public ChestBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //CreateBlockEntity 箱子内容存在方块实体里 对应原版 newBlockEntity
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new ChestBlockEntity(pos);

        //HasBlockEntity 箱子带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        //GetStateForPlacement 落地时与同朝向的邻箱自动连成双箱 对应原版 ChestBlock.getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
        {
            var facing = horizontalFacing.Opposite;
            return DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing, facing.ToState())
                .SetValue(BlockStateProperties.ChestTypeProperty, GetChestType(level, pos, facing));
        }

        //GetChestType 顺时针邻居同朝向则自己是左半 逆时针则是右半 对应原版同名方法
        private static ChestType GetChestType(ServerLevel? level, BlockPos pos, Direction facing)
        {
            if (facing == CandidatePartnerFacing(level, pos, facing.ClockWise)) return ChestType.left;
            if (facing == CandidatePartnerFacing(level, pos, facing.CounterClockWise)) return ChestType.right;
            return ChestType.single;
        }

        //CandidatePartnerFacing 只有单箱邻居才可能合作 返回它的朝向 不合作返回 null
        //没有关卡上下文就没有邻居可看 放置状态退回单人
        private static Direction? CandidatePartnerFacing(ServerLevel? level, BlockPos pos, Direction neighbourDirection)
        {
            if (level is null) return null;
            var state = level.GetBlockState(pos.Offset(neighbourDirection));
            if (state?.Owner is not ChestBlock) return null;
            if (state.Value.GetValue(BlockStateProperties.ChestTypeProperty) != ChestType.single) return null;
            return state.Value.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
        }

        //GetConnectedDirection 另一半所在方向 左半在顺时针 右半在逆时针 对应原版同名方法
        public static Direction GetConnectedDirection(BlockState state)
        {
            var facing = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            return state.GetValue(BlockStateProperties.ChestTypeProperty) == ChestType.left
                ? facing.ClockWise
                : facing.CounterClockWise;
        }

        //UpdateShape 邻箱出现或消失时跟随形态 对应原版 ChestBlock.updateShape
        //方向参数是从本方块指向发起变化的那一格 原版语义同上
        public override BlockState UpdateShape(ServerLevel level, BlockPos pos, BlockState state,
            Direction directionToNeighbour, BlockPos neighbourPos, BlockState neighbourState)
        {
            if (neighbourState.Owner is ChestBlock && directionToNeighbour.IsHorizontal)
            {
                var neighbourType = neighbourState.GetValue(BlockStateProperties.ChestTypeProperty);
                var ownType = state.GetValue(BlockStateProperties.ChestTypeProperty);
                if (ownType == ChestType.single && neighbourType != ChestType.single
                    && state.GetValue(BlockStateProperties.HorizontalFacing)
                        == neighbourState.GetValue(BlockStateProperties.HorizontalFacing)
                    && GetConnectedDirection(neighbourState) == directionToNeighbour.Opposite)
                    return state.SetValue(BlockStateProperties.ChestTypeProperty, Opposite(neighbourType));
            }
            else if (GetConnectedDirection(state) == directionToNeighbour)
            {
                return state.SetValue(BlockStateProperties.ChestTypeProperty, ChestType.single);
            }
            return state;
        }

        //Opposite 双箱的另一半形态 对应原版 ChestType.getOpposite
        private static ChestType Opposite(ChestType type)
            => type == ChestType.left ? ChestType.right : ChestType.left;

        //UseOn 右击打开容器 双箱把两半合成一个六行菜单 对应原版 useWithoutItem 与 getMenuProvider
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (TryGetDouble(level, pos, state) is { } pair)
            {
                player.OpenMenu(new DoubleChestMenuProvider(pair.First, pair.Second));
                return true;
            }
            if (level.GetBlockEntity<ChestBlockEntity>(pos) is not { } chest) return false;
            player.OpenMenu(chest);
            return true;
        }

        //TryGetDouble 取双箱两半 两半形态必须互补 对应原版 DoubleBlockCombiner 的成对判定
        //原版 BlockType 把右半当第一半 左半当第二半 槽号顺序随之确定
        private static (ChestBlockEntity First, ChestBlockEntity Second)? TryGetDouble(
            ServerLevel level, BlockPos pos, BlockState state)
        {
            var type = state.GetValue(BlockStateProperties.ChestTypeProperty);
            if (type == ChestType.single) return null;
            var partnerPos = pos.Offset(GetConnectedDirection(state));
            var partnerState = level.GetBlockState(partnerPos);
            if (partnerState?.Owner is not ChestBlock) return null;
            var partnerType = partnerState.Value.GetValue(BlockStateProperties.ChestTypeProperty);
            if (partnerType == ChestType.single || partnerType == type) return null;
            if (level.GetBlockEntity<ChestBlockEntity>(pos) is not { } self) return null;
            if (level.GetBlockEntity<ChestBlockEntity>(partnerPos) is not { } partner) return null;
            return type == ChestType.right ? (self, partner) : (partner, self);
        }

        //DoubleChestMenuProvider 双箱菜单 标题用原版 container.chestDouble
        private sealed class DoubleChestMenuProvider(ChestBlockEntity first, ChestBlockEntity second) : MenuProvider
        {
            public Component DisplayName => Component.Translatable("container.chestDouble");

            public AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
                => ChestMenu.SixRows(containerId, inventory, new CompoundContainer(first, second));
        }
    }

    //BarrelBlock 木桶 行为与箱子一致只是方块实体类型不同 对应原版 BarrelBlock
    public sealed class BarrelBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 14.0);

        public BarrelBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new BarrelBlockEntity(pos);

        //HasBlockEntity 木桶带方块实体 活塞推不动
        public override bool HasBlockEntity => true;

        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<ChestBlockEntity>(pos) is not { } barrel) return false;
            player.OpenMenu(barrel);
            return true;
        }
    }
}
