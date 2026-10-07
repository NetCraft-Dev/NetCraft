using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Entity;

//AbstractHurtingProjectile fireball-like projectile base class, maps to vanilla net.minecraft.world.entity.projectile.hurtingprojectile.AbstractHurtingProjectile
//These projectiles ignore gravity, only slow down by drag and ignite what they touch in flight
public abstract class AbstractHurtingProjectile : Projectile
{
    protected AbstractHurtingProjectile(EntityType<object> type) : base(type) { }

    //DefaultGravity fireballs ignore gravity, maps to vanilla AbstractHurtingProjectile not adding gravity
    public override double DefaultGravity => 0.0;

    //AirDrag fireball air drag 0.95, maps to vanilla 0.95
    public override double AirDrag => 0.95;

    public override void Tick()
    {
        TickBase();
        CheckLeftOwner();
        ApplyGravityAndInertia();
        var hit = ProjectileUtil.GetHitResult(this);
        if (hit.Type != ProjectileHitType.miss)
        {
            Pos = hit.Location;
            UpdateRotation();
            OnHit(hit);
            return;
        }
        Move(Velocity);
        UpdateRotation();
    }
}

//SmallFireball small fireball, maps to vanilla net.minecraft.world.entity.projectile.hurtingprojectile.SmallFireball
//A thrown fire charge becomes this, it deals 5 damage to mobs and lights a fire in the air next to a hit block
//Vanilla also sets mobs on fire for 5 seconds; this project has no fire ticks, so it only deals damage
public sealed class SmallFireball : AbstractHurtingProjectile
{
    //FireDamage damage dealt to mobs, maps to vanilla 5.0f
    private const float FireDamage = 5f;

    public SmallFireball(EntityType<object> type) : base(type) { }

    protected override void OnHitEntity(ProjectileHitResult hit)
    {
        base.OnHitEntity(hit);
        //The source is the fireball's own position, the target is knocked back along the flight direction
        hit.Entity?.Hurt(FireDamage, Pos);
        Discard();
    }

    protected override void OnHitBlock(ProjectileHitResult hit)
    {
        base.OnHitBlock(hit);
        PlaceFire(hit);
        Discard();
    }

    //PlaceFire lights a fire when the slot outside the hit face is air, maps to the vanilla ignition after a block hit
    private void PlaceFire(ProjectileHitResult hit)
    {
        if (Level is not PersistentServerLevel level) return;
        var pos = hit.BlockPos.Relative(hit.Direction, 1);
        if (level.GetBlockState(pos) is not { } state) return;
        if (!ReferenceEquals(state.Owner, Blocks.AIR)) return;
        level.SetBlock(pos, Blocks.FIRE.DefaultBlockState);
    }
}
