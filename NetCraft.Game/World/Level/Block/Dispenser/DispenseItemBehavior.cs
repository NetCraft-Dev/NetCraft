using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry.Enums;
using NetCraft.Registry.State;
using NetCraft.Storage;
//Direction exists in both Primitives and Registry.Enums; geometry and state each use what they need here
using Direction = NetCraft.Primitives.Direction;
//The namespace segment Items clashes with the registry class, so item instances must go through the alias
using NCItems = NetCraft.Game.World.Items.Items;

namespace NetCraft.Game.World.Level.Block.Dispenser;

//DispenseItemBehavior dispense behavior for one item, maps to vanilla net.minecraft.core.dispenser.DispenseItemBehavior
//The return value is written back to the dispenser slot; consumed items must be subtracted from the stack yourself
public interface DispenseItemBehavior
{
    //Dispense handles one dispense
    ItemStack Dispense(BlockSource source, ItemStack dispensed);
}

//DefaultDispenseItemBehavior default dispense behavior, drops one item in front of the dispenser, maps to the vanilla class of the same name
public class DefaultDispenseItemBehavior : DispenseItemBehavior
{
    //DefaultAccuracy scatter of the drop, maps to vanilla 6
    private const int DefaultAccuracy = 6;

    //Dispense runs the behavior and adds sound and animation, maps to vanilla dispense
    public ItemStack Dispense(BlockSource source, ItemStack dispensed)
    {
        var result = Execute(source, dispensed);
        PlaySound(source);
        PlayAnimation(source);
        return result;
    }

    //Execute default implementation drops one item, maps to vanilla execute
    protected virtual ItemStack Execute(BlockSource source, ItemStack dispensed)
    {
        var direction = FacingOf(source.State).ToPrimitive();
        var position = Blocks.DispenserBlock.GetDispensePosition(source);
        SpawnItem(source.Level, SplitOne(dispensed), DefaultAccuracy, direction, position);
        return dispensed;
    }

    //PlaySound plays the dispense sound, maps to world event 1000 of vanilla playDefaultSound
    protected virtual void PlaySound(BlockSource source)
        => source.Level.LevelEvent(1000, source.Pos, 0);

    //PlayAnimation plays the dispense animation, maps to world event 2000 of vanilla playDefaultAnimation
    protected virtual void PlayAnimation(BlockSource source)
        => source.Level.LevelEvent(2000, source.Pos, FacingOf(source.State).ToPrimitive().Id3D);

    //SpawnItem drops the item entity at the landing point, initial velocity along the facing with triangular spread, maps to vanilla spawnItem
    public static void SpawnItem(ServerLevel level, ItemStack stack, int accuracy, Direction direction, Vec3 position)
    {
        if (stack.IsEmpty() || level is not PersistentServerLevel persistent) return;
        //For vertical facings the spawn point drops less to avoid landing at its own feet, maps to the 0.125 versus 0.15625 branch in vanilla
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

    //SplitOne takes one from the stack and decrements it, maps to vanilla ItemStack.split(1)
    protected static ItemStack SplitOne(ItemStack stack)
    {
        if (stack.IsEmpty()) return ItemStack.Empty;
        var one = stack.CopyWithCount(1);
        stack.Shrink(1);
        return one;
    }

    //FacingOf reads the facing from the block state; block states use Registry.Enums.Direction
    protected static NetCraft.Registry.Enums.Direction FacingOf(BlockState state)
        => state.GetValue(BlockStateProperties.FacingProperty);

    //Triangle triangular distribution random, maps to vanilla RandomSource.triangle
    private static double Triangle(double mean, double deviation)
        => mean + deviation * (Random.Shared.NextDouble() - Random.Shared.NextDouble());
}

//ProjectileDispenseBehavior shoots projectile items, maps to vanilla ProjectileDispenseBehavior
//Landing point, initial velocity and spread all come from the item's own DispenseConfig
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

    //PlaySound projectile dispenses use the dispenser shoot sound; an event id in the config takes priority, maps to vanilla playSound
    protected override void PlaySound(BlockSource source)
        => source.Level.LevelEvent(_config.OverrideDispenseEvent ?? 1002, source.Pos, 0);
}

//DispenseBehaviors built-in dispense behavior registration, maps to vanilla DispenseItemBehavior.bootStrap
//Here only items that can actually fire are registered; potions, fireworks, boats, minecarts, spawn eggs and the like wait until the matching entity and component systems land
public static class DispenseBehaviors
{
    //Bootstrap attaches dispense behaviors to projectile items; must be called after the item registry is filled
    public static void Bootstrap()
    {
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.ARROW);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.SNOWBALL);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.EGG);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.ENDER_PEARL);
        Blocks.DispenserBlock.RegisterProjectileBehavior((ProjectileItem)NCItems.FIRE_CHARGE);
    }
}
