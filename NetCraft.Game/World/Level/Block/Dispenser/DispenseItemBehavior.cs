using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
//方向同时存在于 Primitives 与 Registry.Enums 这里几何与状态各取所需
using Direction = NetCraft.Primitives.Direction;
//命名空间段 Items 与注册表类同名 取物品实例必须走别名
using NCItems = NetCraft.Game.World.Items.Items;

namespace NetCraft.Game.World.Level.Block.Dispenser;

//DispenseItemBehavior 发射器对某件物品的发射行为 对应原版 net.minecraft.core.dispenser.DispenseItemBehavior
//返回值会写回发射器槽位 消耗掉的物品要自己从栈里扣
public interface DispenseItemBehavior
{
    //Dispense 处理一次发射
    ItemStack Dispense(BlockSource source, ItemStack dispensed);
}

//DefaultDispenseItemBehavior 默认发射行为 把一件物品丢在发射器前方 对应原版同名类
public class DefaultDispenseItemBehavior : DispenseItemBehavior
{
    //DefaultAccuracy 丢出物的散射程度 对应原版 6
    private const int DefaultAccuracy = 6;

    //Dispense 执行行为并补上声音与动画 对应原版 dispense
    public ItemStack Dispense(BlockSource source, ItemStack dispensed)
    {
        var result = Execute(source, dispensed);
        PlaySound(source);
        PlayAnimation(source);
        return result;
    }

    //Execute 默认实现丢出一件物品 对应原版 execute
    protected virtual ItemStack Execute(BlockSource source, ItemStack dispensed)
    {
        var direction = FacingOf(source.State).ToPrimitive();
        var position = Blocks.DispenserBlock.GetDispensePosition(source);
        SpawnItem(source.Level, SplitOne(dispensed), DefaultAccuracy, direction, position);
        return dispensed;
    }

    //PlaySound 播发射音效 对应原版 playDefaultSound 的世界事件 1000
    protected virtual void PlaySound(BlockSource source)
        => source.Level.LevelEvent(1000, source.Pos, 0);

    //PlayAnimation 播发射动画 对应原版 playDefaultAnimation 的世界事件 2000
    protected virtual void PlayAnimation(BlockSource source)
        => source.Level.LevelEvent(2000, source.Pos, FacingOf(source.State).ToPrimitive().Id3D);

    //SpawnItem 把物品实体丢在落点上 初速按朝向加三角分布散布 对应原版 spawnItem
    public static void SpawnItem(ServerLevel level, ItemStack stack, int accuracy, Direction direction, Vec3 position)
    {
        if (stack.IsEmpty() || level is not PersistentServerLevel persistent) return;
        //竖直朝向时落点少降一点 免得贴着自己脚下 对应原版那个 0.125 与 0.15625 的分支
        var spawnY = direction.AxisValue == Direction.Axis.Y ? position.Y - 0.125 : position.Y - 0.15625;
        var drop = new ItemEntity(EntityTypes.ITEM, position.X, spawnY, position.Z, stack);
        var pow = Random.Shared.NextDouble() * 0.1 + 0.2;
        var deviation = 0.0172275 * accuracy;
        drop.Velocity = new Vec3(
            Triangle(direction.StepX * pow, deviation),
            Triangle(0.2, deviation),
            Triangle(direction.StepZ * pow, deviation));
        drop.SetDefaultPickUpDelay();
        persistent.AddEntity(drop);
    }

    //SplitOne 从栈里取出一件并扣减原栈 对应原版 ItemStack.split(1)
    protected static ItemStack SplitOne(ItemStack stack)
    {
        if (stack.IsEmpty()) return ItemStack.Empty;
        var one = stack.CopyWithCount(1);
        stack.Shrink(1);
        return one;
    }

    //FacingOf 读方块状态里的朝向 方块状态用 Registry.Enums.Direction
    protected static NetCraft.Registry.Enums.Direction FacingOf(BlockState state)
        => state.GetValue(BlockStateProperties.FacingProperty);

    //Triangle 三角分布随机 对应原版 RandomSource.triangle
    private static double Triangle(double mean, double deviation)
        => mean + deviation * (Random.Shared.NextDouble() - Random.Shared.NextDouble());
}

//ProjectileDispenseBehavior 把投射物类物品射出去 对应原版 ProjectileDispenseBehavior
//落点 初速 散布都取自物品自己的 DispenseConfig
public sealed class ProjectileDispenseBehavior : DefaultDispenseItemBehavior
{
    private readonly ProjectileItem _item;
    private readonly DispenseConfig _config;

    public ProjectileDispenseBehavior(ProjectileItem item)
    {
        _item = item;
        _config = item.CreateDispenseConfig();
    }

    protected override ItemStack Execute(BlockSource source, ItemStack dispensed)
    {
        var direction = FacingOf(source.State).ToPrimitive();
        var position = _config.PositionFunction(source, direction);
        var projectile = _item.AsProjectile(position, dispensed);
        _item.Shoot(projectile, direction.StepX, direction.StepY, direction.StepZ, _config.Power, _config.Uncertainty);
        if (source.Level is PersistentServerLevel level) level.AddEntity(projectile);
        dispensed.Shrink(1);
        return dispensed;
    }

    //PlaySound 投射物类发射换成发射器射击音效 配置里指定事件号时优先 对应原版 playSound
    protected override void PlaySound(BlockSource source)
        => source.Level.LevelEvent(_config.OverrideDispenseEvent ?? 1002, source.Pos, 0);
}

//DispenseBehaviors 内置发射行为登记 对应原版 DispenseItemBehavior.bootStrap
//本作只登记能真的射出去的物品 药水 烟花 船 矿车 刷怪蛋等要等对应实体与组件体系接入再补
public static class DispenseBehaviors
{
    //Bootstrap 给投射物类物品挂发射行为 必须在物品注册表填好之后调
    public static void Bootstrap()
    {
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.ARROW);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.SNOWBALL);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.EGG);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.ENDER_PEARL);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.FIRE_CHARGE);
    }
}
