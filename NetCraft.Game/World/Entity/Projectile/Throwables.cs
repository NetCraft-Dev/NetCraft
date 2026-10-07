using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Entity;

//Snowball snowball, maps to vanilla net.minecraft.world.entity.projectile.throwableitemprojectile.Snowball
//Shatters on impact and deals no damage to normal mobs; vanilla deals 3 to blazes, but this project has no blazes so damage is always zero
public sealed class Snowball : ThrowableItemProjectile
{
    public Snowball(EntityType<object> type) : base(type) { }

    protected override void OnHit(ProjectileHitResult hit)
    {
        base.OnHit(hit);
        Discard();
    }
}

//ThrownEgg egg, maps to vanilla net.minecraft.world.entity.projectile.throwableitemprojectile.ThrownEgg
//Has a small chance to hatch a chick on impact
public sealed class ThrownEgg : ThrowableItemProjectile
{
    //HatchChance reciprocal chance of hatching a chick on impact, maps to vanilla 1/8
    private const int HatchChance = 8;

    public ThrownEgg(EntityType<object> type) : base(type) { }

    protected override void OnHit(ProjectileHitResult hit)
    {
        base.OnHit(hit);
        if (Level is PersistentServerLevel level && Random.Shared.Next(HatchChance) == 0)
        {
            if (EntityTypes.CHICKEN.Create(level) is { } chicken)
            {
                chicken.Pos = Pos;
                level.AddEntity(chicken);
            }
        }
        Discard();
    }
}

//ThrownEnderpearl ender pearl, maps to vanilla net.minecraft.world.entity.projectile.throwableitemprojectile.ThrownEnderpearl
//On impact it moves the owner to the landing point; vanilla also deals 5 damage to the owner and grants a chunk loading ticket, the latter is absent here
//Players are not in the level entity set, so a hand-thrown ender pearl cannot find its owner for now; this is added once player entities land
public sealed class ThrownEnderpearl : ThrowableItemProjectile
{
    public ThrownEnderpearl(EntityType<object> type) : base(type) { }

    protected override void OnHit(ProjectileHitResult hit)
    {
        base.OnHit(hit);
        //The landing point uses the position before movement to avoid stuffing the owner into the block at the hit point, maps to vanilla oldPosition
        var teleportPos = PreviousPos;
        if (Level is PersistentServerLevel level && FindOwner(level) is { } owner)
        {
            owner.Pos = teleportPos;
            owner.Velocity = Vec3.Zero;
        }
        Discard();
    }

    //FindOwner finds the owner again by uuid in the level entity set
    private NetCraft.Registry.Entity? FindOwner(PersistentServerLevel level)
    {
        if (OwnerUuid is not { } uuid) return null;
        foreach (var entity in level.Entities)
            if (entity.Uuid == uuid) return entity;
        return null;
    }
}
