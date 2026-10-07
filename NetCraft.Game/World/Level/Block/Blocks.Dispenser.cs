using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block.Dispenser;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NetCraft.Util.Random;
using Direction = NetCraft.Primitives.Direction;

namespace NetCraft.Game.World.Level.Block;

//P-2 dispenser and dropper, maps to vanilla net.minecraft.world.level.block.DispenserBlock and DropperBlock
//They share one block entity and menu, differing only in how the behavior is chosen on dispensing; the dropper prefers moving into a container in front
//Dispense behaviors are in Dispenser.DispenseItemBehavior, projectile entities are the Projectile family under World.Entity
public static partial class Blocks
{
    public static readonly DispenserBlock DISPENSER = new("dispenser");
    public static readonly DropperBlock DROPPER = new("dropper");

    //RegisterDispenser registers the dispenser family into the real block table
    private static void RegisterDispenser(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { DISPENSER, DROPPER };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //DispenserBlock dispenser, maps to vanilla DispenserBlock
    //On the rising signal edge it schedules a tick four ticks later, which takes one item from a random non-empty slot and dispenses it per the behavior
    public class DispenserBlock : NamedBlock
    {
        //TriggerDuration ticks between receiving the signal and dispensing, maps to vanilla TRIGGER_DURATION
        public const int TriggerDuration = 4;

        //EmptyDispenseEvent world event when an empty dispenser is activated, maps to vanilla 1001
        protected const int EmptyDispenseEvent = 1001;

        //DefaultBehavior used by items without a dedicated behavior, thrown out as is
        protected static readonly DefaultDispenseItemBehavior DefaultBehavior = new();

        //_behaviors map from item to dispense behavior, maps to vanilla DISPENSER_REGISTRY
        private static readonly Dictionary<Item, DispenseItemBehavior> Behaviors = new();

        public DispenserBlock(string name) : base(name) { }

        public override bool HasBlockEntity => true;

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new DispenserBlockEntity(pos);

        //RegisterBehavior attaches a dispense behavior to an item, maps to vanilla registerBehavior
        public static void RegisterBehavior(Item item, DispenseItemBehavior behavior) => Behaviors[item] = behavior;

        //RegisterProjectileBehavior attaches a dispense behavior to a projectile item, maps to vanilla registerProjectileBehavior
        public static void RegisterProjectileBehavior(ProjectileItem item)
        {
            if (item is Item plain) Behaviors[plain] = new ProjectileDispenseBehavior(item);
        }

        //GetDispensePosition default spawn point, block center moved 0.7 blocks along the facing, maps to the vanilla single-argument overload
        public static Vec3 GetDispensePosition(BlockSource source)
            => GetDispensePosition(source, 0.7, Vec3.Zero);

        //GetDispensePosition spawn point with distance and offset, maps to the vanilla three-argument overload
        public static Vec3 GetDispensePosition(BlockSource source, double scale, Vec3 offset)
        {
            var direction = FacingOf(source.State).ToPrimitive();
            var center = source.Center;
            return new Vec3(
                center.X + scale * direction.StepX + offset.X,
                center.Y + scale * direction.StepY + offset.Y,
                center.Z + scale * direction.StepZ + offset.Z);
        }

        //GetStateForPlacement the facing is opposite the player's looking direction, maps to vanilla getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState
                .SetValue(BlockStateProperties.FacingProperty, lookingDirection.Opposite.ToState())
                .SetValue(BlockStateProperties.Triggered, false);

        //NeighborChanged schedules a dispense four ticks later on the rising edge and only clears the flag on the falling edge, maps to vanilla neighborChanged
        //Signals on itself and the block above both count, since dispensers are often fed by redstone wire above
        public override void NeighborChanged(ServerLevel level, BlockPos pos, BlockState state,
            NetCraft.Registry.Block changedBlock, bool movedByPiston)
        {
            var shouldTrigger = level.HasNeighborSignal(pos) || level.HasNeighborSignal(pos.Offset(Direction.Up));
            var triggered = state.GetValue(BlockStateProperties.Triggered);
            if (shouldTrigger && !triggered)
            {
                level.ScheduleTick(pos, this, TriggerDuration);
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Triggered, true), 2);
            }
            else if (!shouldTrigger && triggered)
            {
                level.SetBlock(pos, state.SetValue(BlockStateProperties.Triggered, false), 2);
            }
        }

        //Tick dispenses when the scheduled tick fires, maps to vanilla tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
            => DispenseFrom(level, state, pos);

        //DispenseFrom takes one item from a random non-empty slot and hands it to the behavior it picks, maps to vanilla dispenseFrom
        protected virtual void DispenseFrom(ServerLevel level, BlockState state, BlockPos pos)
        {
            if (level.GetBlockEntity<DispenserBlockEntity>(pos) is not { } blockEntity) return;
            var source = new BlockSource(level, pos, state, blockEntity);
            var slot = blockEntity.GetRandomSlot(Random.Shared);
            if (slot < 0)
            {
                level.LevelEvent(EmptyDispenseEvent, pos, 0);
                return;
            }
            var stack = blockEntity.GetItem(slot);
            var behavior = GetDispenseMethod(level, stack);
            blockEntity.SetItem(slot, behavior.Dispense(source, stack));
        }

        //GetDispenseMethod looks up the dispense behavior by item, unregistered ones use the default throw, maps to vanilla getDispenseMethod
        protected virtual DispenseItemBehavior GetDispenseMethod(ServerLevel level, ItemStack stack)
            => !stack.IsEmpty() && Behaviors.TryGetValue(stack.GetItem(), out var behavior)
                ? behavior
                : DefaultBehavior;

        //UseOn right click opens the nine-slot menu, maps to vanilla useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<DispenserBlockEntity>(pos) is not { } blockEntity) return false;
            player.OpenMenu(blockEntity);
            return true;
        }

        //AffectNeighborsAfterRemoval notifies neighbors to recompute after removal, maps to vanilla affectNeighborsAfterRemoval
        //A dispenser may feed a comparator, so it must re-read after being broken
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
            => Containers.UpdateNeighboursAfterDestroy(state, level, pos);

        public override bool HasAnalogOutputSignal => true;

        //GetAnalogOutputSignal a comparator reads the fullness of the dispenser contents, maps to vanilla getAnalogOutputSignal
        public override int GetAnalogOutputSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => AbstractContainerMenu.GetRedstoneSignalFromBlockEntity(level.GetBlockEntity<DispenserBlockEntity>(pos));

        //FacingOf reads the facing from the block state, block states use Registry.Enums.Direction
        protected static NetCraft.Registry.Enums.Direction FacingOf(BlockState state)
            => state.GetValue(BlockStateProperties.FacingProperty);
    }

    //DropperBlock dropper, maps to vanilla DropperBlock
    //On dispensing it first checks for a container in the cell ahead; if present it moves one item in, otherwise it throws per the default behavior
    public sealed class DropperBlock : DispenserBlock
    {
        public DropperBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new DropperBlockEntity(pos);

        //GetDispenseMethod the dropper always uses the default throw behavior, maps to the vanilla override
        protected override DispenseItemBehavior GetDispenseMethod(ServerLevel level, ItemStack stack) => DefaultBehavior;

        protected override void DispenseFrom(ServerLevel level, BlockState state, BlockPos pos)
        {
            if (level.GetBlockEntity<DispenserBlockEntity>(pos) is not { } blockEntity) return;
            var source = new BlockSource(level, pos, state, blockEntity);
            var slot = blockEntity.GetRandomSlot(Random.Shared);
            if (slot < 0)
            {
                level.LevelEvent(EmptyDispenseEvent, pos, 0);
                return;
            }
            var stack = blockEntity.GetItem(slot);
            var direction = FacingOf(state).ToPrimitive();
            var targetPos = pos.Offset(direction);
            //Moves one item if the block ahead is a container, throws only when it cannot move, maps to vanilla dispenseFrom
            if (level.GetBlockEntity<Container>(targetPos) is { } into)
            {
                var moved = ContainerHelper.AddItem(into, stack.CopyWithCount(1));
                if (moved.IsEmpty())
                {
                    var remaining = stack.Copy();
                    remaining.Shrink(1);
                    blockEntity.SetItem(slot, remaining);
                    return;
                }
            }
            blockEntity.SetItem(slot, DefaultBehavior.Dispense(source, stack));
        }
    }
}
