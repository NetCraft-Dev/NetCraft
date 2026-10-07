using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.Block.Dispenser;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Items;

//DispenseConfig dispenser launch parameters, maps to vanilla net.minecraft.world.item.ProjectileItem.DispenseConfig
//Position function, spread, initial speed, and the event id overriding the default launch sound
public sealed record DispenseConfig(
    Func<BlockSource, NetCraft.Primitives.Direction, Vec3> PositionFunction,
    float Uncertainty,
    float Power,
    int? OverrideDispenseEvent)
{
    //Default default launch parameters: spawns 0.7 blocks in front of the center raised by 0.1, spread 6, initial speed 1.1, maps to vanilla DispenseConfig.DEFAULT
    public static readonly DispenseConfig Default = new(
        (source, direction) => Blocks.DispenserBlock.GetDispensePosition(source, 0.7, new Vec3(0.0, 0.1, 0.0)),
        6.0f,
        1.1f,
        null);
}

//ProjectileItem an item that can be shot as a projectile, maps to vanilla net.minecraft.world.item.ProjectileItem
//The dispenser spawns entities through it, player throwing will use the same path
public interface ProjectileItem
{
    //AsProjectile creates the projectile entity to be launched and fills in position and carried item, maps to vanilla asProjectile
    Projectile AsProjectile(Vec3 position, ItemStack stack);

    //CreateDispenseConfig launch parameters, maps to vanilla createDispenseConfig
    DispenseConfig CreateDispenseConfig() => DispenseConfig.Default;

    //Shoot turns the direction components into velocity on the projectile, maps to vanilla shoot
    void Shoot(Projectile projectile, double xd, double yd, double zd, float pow, float uncertainty)
        => projectile.Shoot(xd, yd, zd, pow, uncertainty);

    //Use right-click throwing while the player holds the item, maps to the use of vanilla snowball-like items
    void Use(PersistentServerLevel level, ServerPlayer player, ItemStack stack);
}

//ProjectileItemBase base class for projectile items, unifies the registry name and entity construction
public abstract class ProjectileItemBase : Item, ProjectileItem
{
    private readonly string _name;

    protected ProjectileItemBase(string name) => _name = name;

    public override Identifier Id => Identifier.WithDefaultNamespace(_name);

    //AsProjectile creates the entity and fills position and carried item; projectiles fired by a dispenser have no owner, same as vanilla
    public Projectile AsProjectile(Vec3 position, ItemStack stack)
    {
        var projectile = CreateProjectile();
        projectile.Pos = position;
        if (projectile is ThrowableItemProjectile itemProjectile) itemProjectile.Item = stack;
        return projectile;
    }

    public virtual DispenseConfig CreateDispenseConfig() => DispenseConfig.Default;

    //ThrowPower initial speed of a player throw, maps to vanilla PROJECTILE_SHOOT_POWER 1.5
    protected virtual float ThrowPower => 1.5f;

    //ThrowUncertainty spread of a player throw, maps to vanilla 1.0
    protected virtual float ThrowUncertainty => 1.0f;

    //Use spawns the projectile 0.1 blocks below the eyes and shoots it along the view direction; consumption and syncing are left to the caller
    public virtual void Use(PersistentServerLevel level, ServerPlayer player, ItemStack stack)
    {
        var position = new Vec3(player.Position.X, player.Position.Y + ServerPlayer.EyeHeight - 0.1,
            player.Position.Z);
        var projectile = AsProjectile(position, stack);
        projectile.SetOwner(player.Uuid);
        var (xd, yd, zd) = LookVector(player.Yaw, player.Pitch);
        Shoot(projectile, xd, yd, zd, ThrowPower, ThrowUncertainty);
        level.AddEntity(projectile);
    }

    //Shoot implements ProjectileItem.Shoot so it can also be called directly within the class
    public virtual void Shoot(Projectile projectile, double xd, double yd, double zd, float pow,
        float uncertainty)
        => projectile.Shoot(xd, yd, zd, pow, uncertainty);

    //LookVector computes the view unit vector from the rotation, maps to the trig at the start of vanilla Entity.shootFromRotation
    private static (double X, double Y, double Z) LookVector(float yaw, float pitch)
    {
        var yawRad = yaw * 0.017453292f;
        var pitchRad = pitch * 0.017453292f;
        return (-Mth.Sin(yawRad) * Mth.Cos(pitchRad),
            -Mth.Sin(pitchRad),
            Mth.Cos(yawRad) * Mth.Cos(pitchRad));
    }

    //CreateProjectile creates the concrete projectile entity, the entity type is bound by subclasses
    protected abstract Projectile CreateProjectile();
}

//ArrowItem arrow, maps to vanilla ArrowItem
public sealed class ArrowItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new Arrow(EntityTypes.ARROW);
}

//SnowballItem snowball, maps to vanilla SnowballItem
public sealed class SnowballItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new Snowball(EntityTypes.SNOWBALL);
}

//EggItem egg, maps to vanilla EggItem
public sealed class EggItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new ThrownEgg(EntityTypes.EGG);
}

//EnderPearlItem ender pearl, maps to vanilla EnderpearlItem
public sealed class EnderPearlItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new ThrownEnderpearl(EntityTypes.ENDER_PEARL);
}

//FireChargeItem fire charge, maps to the projectile part of vanilla FireChargeItem
public sealed class FireChargeItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new SmallFireball(EntityTypes.SMALL_FIREBALL);

    //A fire charge spawns farther with less initial speed and uses the fire sound, maps to vanilla createDispenseConfig
    public override DispenseConfig CreateDispenseConfig() => new(
        (source, direction) => Blocks.DispenserBlock.GetDispensePosition(source, 1.0, Vec3.Zero),
        6.6666665f,
        1.0f,
        1018);
}
