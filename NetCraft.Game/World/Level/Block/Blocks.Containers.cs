using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Network.Chat;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
//Direction in the property enums clashes with Primitives.Direction, so only the needed chest type is imported
using ChestType = NetCraft.Registry.Enums.ChestType;

namespace NetCraft.Game.World.Level.Block;

//V-8 container blocks; opening a screen goes through MenuProvider, the server sends open_screen and the client opens the screen by menu type
public static partial class Blocks
{
    public static readonly ChestBlock CHEST = new("chest");
    public static readonly BarrelBlock BARREL = new("barrel");

    //RegisterContainers registers container blocks into the real block table
    private static void RegisterContainers(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { CHEST, BARREL };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //ChestBlock chest, maps to vanilla net.minecraft.world.level.block.ChestBlock
    //Two chests with the same facing automatically form a double chest; the target container is combined from both halves, and the menu has six rows
    public sealed class ChestBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 14.0);

        public ChestBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        //CreateBlockEntity chest contents live in the block entity, maps to vanilla newBlockEntity
        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new ChestBlockEntity(pos);

        //HasBlockEntity chest has a block entity, pistons cannot push it
        public override bool HasBlockEntity => true;

        //GetStateForPlacement pairs with a same-facing neighbor chest into a double chest on landing, maps to vanilla ChestBlock.getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
        {
            var facing = horizontalFacing.Opposite;
            return DefaultBlockState
                .SetValue(BlockStateProperties.HorizontalFacing, facing.ToState())
                .SetValue(BlockStateProperties.ChestTypeProperty, GetChestType(level, pos, facing));
        }

        //GetChestType the clockwise neighbor with the same facing makes this the left half, counterclockwise makes it the right half, maps to the vanilla method of the same name
        private static ChestType GetChestType(ServerLevel? level, BlockPos pos, Direction facing)
        {
            if (facing == CandidatePartnerFacing(level, pos, facing.ClockWise)) return ChestType.left;
            if (facing == CandidatePartnerFacing(level, pos, facing.CounterClockWise)) return ChestType.right;
            return ChestType.single;
        }

        //CandidatePartnerFacing only a single-chest neighbor can pair; returns its facing, or null when it cannot pair
        //Without a level context there are no neighbors to inspect, so the placement state falls back to single
        private static Direction? CandidatePartnerFacing(ServerLevel? level, BlockPos pos, Direction neighbourDirection)
        {
            if (level is null) return null;
            var state = level.GetBlockState(pos.Offset(neighbourDirection));
            if (state?.Owner is not ChestBlock) return null;
            if (state.Value.GetValue(BlockStateProperties.ChestTypeProperty) != ChestType.single) return null;
            return state.Value.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
        }

        //GetConnectedDirection direction of the other half: left half is clockwise, right half is counterclockwise, maps to the vanilla method of the same name
        public static Direction GetConnectedDirection(BlockState state)
        {
            var facing = state.GetValue(BlockStateProperties.HorizontalFacing).ToPrimitive();
            return state.GetValue(BlockStateProperties.ChestTypeProperty) == ChestType.left
                ? facing.ClockWise
                : facing.CounterClockWise;
        }

        //UpdateShape follows the form when the neighbor chest appears or disappears, maps to vanilla ChestBlock.updateShape
        //The direction parameter points from this block toward the cell that changed, same vanilla semantics as above
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

        //Opposite the other half of a double chest, maps to vanilla ChestType.getOpposite
        private static ChestType Opposite(ChestType type)
            => type == ChestType.left ? ChestType.right : ChestType.left;

        //UseOn right-click opens the container; a double chest combines both halves into one six-row menu, maps to vanilla useWithoutItem and getMenuProvider
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

        //TryGetDouble gets both halves of a double chest; the two halves must be complementary, maps to the pairing check of vanilla DoubleBlockCombiner
        //Vanilla BlockType treats the right half as the first half and the left half as the second, which fixes the slot order
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

        //DoubleChestMenuProvider double chest menu, title uses vanilla container.chestDouble
        private sealed class DoubleChestMenuProvider(ChestBlockEntity first, ChestBlockEntity second) : MenuProvider
        {
            public Component DisplayName => Component.Translatable("container.chestDouble");

            public AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player)
                => ChestMenu.SixRows(containerId, inventory, new CompoundContainer(first, second));
        }
    }

    //BarrelBlock barrel, behavior identical to a chest with a different block entity type, maps to vanilla BarrelBlock
    public sealed class BarrelBlock : NamedBlock
    {
        private static readonly VoxelShape Shape = NetCraft.Registry.Block.Column(14.0, 0.0, 14.0);

        public BarrelBlock(string name) : base(name) { }

        public override VoxelShape GetShape(BlockState state, BlockGetter level, BlockPos pos, CollisionContext context)
            => Shape;

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state) => new BarrelBlockEntity(pos);

        //HasBlockEntity barrel has a block entity, pistons cannot push it
        public override bool HasBlockEntity => true;

        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<ChestBlockEntity>(pos) is not { } barrel) return false;
            player.OpenMenu(barrel);
            return true;
        }
    }
}
