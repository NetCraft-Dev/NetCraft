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

//P-2 发射器与投掷器 对应原版 net.minecraft.world.level.block.DispenserBlock 与 DropperBlock
//两者共用一个方块实体与菜单 差别只在发射时选行为的方式 投掷器优先往前方的容器里搬
//发射行为见 Dispenser.DispenseItemBehavior 投射物实体见 World.Entity 下 Projectile 系列
public static partial class Blocks
{
    public static readonly DispenserBlock DISPENSER = new("dispenser");
    public static readonly DropperBlock DROPPER = new("dropper");

    //RegisterDispenser 发射器系列登记进真实方块表
    private static void RegisterDispenser(Dictionary<string, BlockBehaviour> real)
    {
        BlockBehaviour[] blocks = { DISPENSER, DROPPER };
        foreach (var block in blocks) real[block.Id.Path] = block;
    }

    //DispenserBlock 发射器 对应原版 DispenserBlock
    //信号上升沿排一个四刻后的调度刻 到点从随机一个非空槽取一件按行为发射
    public class DispenserBlock : NamedBlock
    {
        //TriggerDuration 收到信号到发射之间等待的刻数 对应原版 TRIGGER_DURATION
        public const int TriggerDuration = 4;

        //EmptyDispenseEvent 空发射器被激活的世界事件 对应原版 1001
        protected const int EmptyDispenseEvent = 1001;

        //DefaultBehavior 没有登记专用行为的物品都走它 原样丢出
        protected static readonly DefaultDispenseItemBehavior DefaultBehavior = new();

        //_behaviors 物品到发射行为的表 对应原版 DISPENSER_REGISTRY
        private static readonly Dictionary<Item, DispenseItemBehavior> Behaviors = new();

        public DispenserBlock(string name) : base(name) { }

        public override bool HasBlockEntity => true;

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new DispenserBlockEntity(pos);

        //RegisterBehavior 给某件物品挂发射行为 对应原版 registerBehavior
        public static void RegisterBehavior(Item item, DispenseItemBehavior behavior) => Behaviors[item] = behavior;

        //RegisterProjectileBehavior 给投射物类物品挂发射行为 对应原版 registerProjectileBehavior
        public static void RegisterProjectileBehavior(ProjectileItem item)
        {
            if (item is Item plain) Behaviors[plain] = new ProjectileDispenseBehavior(item);
        }

        //GetDispensePosition 默认落点 方块中心沿朝向前移 0.7 格 对应原版单参重载
        public static Vec3 GetDispensePosition(BlockSource source)
            => GetDispensePosition(source, 0.7, Vec3.Zero);

        //GetDispensePosition 带缩距与偏移的落点 对应原版三参重载
        public static Vec3 GetDispensePosition(BlockSource source, double scale, Vec3 offset)
        {
            var direction = FacingOf(source.State).ToPrimitive();
            var center = source.Center;
            return new Vec3(
                center.X + scale * direction.StepX + offset.X,
                center.Y + scale * direction.StepY + offset.Y,
                center.Z + scale * direction.StepZ + offset.Z);
        }

        //GetStateForPlacement 朝向取玩家视线反方向 对应原版 getStateForPlacement
        public override BlockState? GetStateForPlacement(ServerLevel level, BlockPos pos, Direction face,
            Direction horizontalFacing, Direction lookingDirection)
            => DefaultBlockState
                .SetValue(BlockStateProperties.FacingProperty, lookingDirection.Opposite.ToState())
                .SetValue(BlockStateProperties.Triggered, false);

        //NeighborChanged 信号上升沿排一个四刻后的发射 下降沿只清掉标记 对应原版 neighborChanged
        //自身与上方那一格的信号都算 因为发射器常被上方红石线压着
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

        //Tick 调度刻到点就发射 对应原版 tick
        public override void Tick(ServerLevel level, BlockPos pos, BlockState state, RandomSource random)
            => DispenseFrom(level, state, pos);

        //DispenseFrom 从随机一个非空槽取一件交给自己选的发射行为 对应原版 dispenseFrom
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

        //GetDispenseMethod 按物品查发射行为 没登记就走默认丢出 对应原版 getDispenseMethod
        protected virtual DispenseItemBehavior GetDispenseMethod(ServerLevel level, ItemStack stack)
            => !stack.IsEmpty() && Behaviors.TryGetValue(stack.GetItem(), out var behavior)
                ? behavior
                : DefaultBehavior;

        //UseOn 右击打开九格菜单 对应原版 useWithoutItem
        public override bool UseOn(ServerLevel level, ServerPlayer player, BlockPos pos, BlockState state, Direction face)
        {
            if (level.GetBlockEntity<DispenserBlockEntity>(pos) is not { } blockEntity) return false;
            player.OpenMenu(blockEntity);
            return true;
        }

        //AffectNeighborsAfterRemoval 拆掉后通知邻居重算 对应原版 affectNeighborsAfterRemoval
        //发射器可能是比较器的输入源 拆掉后要让比较器重读
        public override void AffectNeighborsAfterRemoval(ServerLevel level, BlockPos pos, BlockState state,
            bool movedByPiston)
            => Containers.UpdateNeighboursAfterDestroy(state, level, pos);

        public override bool HasAnalogOutputSignal => true;

        //GetAnalogOutputSignal 比较器读发射器内容物的填充度 对应原版 getAnalogOutputSignal
        public override int GetAnalogOutputSignal(ServerLevel level, BlockPos pos, BlockState state, Direction direction)
            => AbstractContainerMenu.GetRedstoneSignalFromBlockEntity(level.GetBlockEntity<DispenserBlockEntity>(pos));

        //FacingOf 读方块状态里的朝向 方块状态用 Registry.Enums.Direction
        protected static NetCraft.Registry.Enums.Direction FacingOf(BlockState state)
            => state.GetValue(BlockStateProperties.FacingProperty);
    }

    //DropperBlock 投掷器 对应原版 DropperBlock
    //发射时先看前方那格有没有容器 有就搬一件进去 没有才按默认行为丢出来
    public sealed class DropperBlock : DispenserBlock
    {
        public DropperBlock(string name) : base(name) { }

        public override BlockEntity? CreateBlockEntity(BlockPos pos, BlockState state)
            => new DropperBlockEntity(pos);

        //GetDispenseMethod 投掷器一律用默认丢出行为 对应原版覆写
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
            //前方是容器就搬一件过去 搬不动才丢出来 对应原版 dispenseFrom
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
