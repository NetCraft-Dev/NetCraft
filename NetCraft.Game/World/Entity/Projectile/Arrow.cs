using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Entity;

//AbstractArrow arrow-like projectile base class, maps to vanilla net.minecraft.world.entity.projectile.arrow.AbstractArrow
//Gravity 0.05, air drag 0.99, sticks in place on hitting a block and despawns after a while
//Vanilla also has pickup (players can retrieve arrows) and piercing enchantment; this project has no player item use system, so only sticking and despawning are kept
public abstract class AbstractArrow : Projectile
{
    //BaseDamage base arrow damage, maps to vanilla default 2.0, the final damage is multiplied by flight speed
    private const double BaseDamage = 2.0;

    //AirborneDespawnTicks ticks it survives after sticking, maps to vanilla 1200 ticks (60 seconds)
    private const int AirborneDespawnTicks = 1200;

    private bool _inGround;
    private int _inGroundTime;

    protected AbstractArrow(EntityType<object> type) : base(type) { }

    //DefaultGravity arrow gravity 0.05, maps to vanilla getDefaultGravity
    public override double DefaultGravity => 0.05;

    //AirDrag arrow air drag 0.99, maps to vanilla getAirDrag
    public override double AirDrag => 0.99;

    //InGround whether the arrow is stuck in a block
    public bool InGround => _inGround;

    public override void Tick()
    {
        TickBase();
        CheckLeftOwner();
        if (_inGround)
        {
            _inGroundTime++;
            if (_inGroundTime >= AirborneDespawnTicks) Discard();
            return;
        }
        ApplyGravityAndInertia();
        var hit = ProjectileUtil.GetHitResult(this);
        if (hit.Type == ProjectileHitType.entity)
        {
            Pos = hit.Location;
            UpdateRotation();
            OnHit(hit);
            return;
        }
        if (hit.Type == ProjectileHitType.block)
        {
            //Hitting a block sticks it, the position stops at the hit point, maps to the vanilla block hit handling
            Pos = hit.Location;
            _inGround = true;
            Velocity = Vec3.Zero;
            UpdateRotation();
            OnHit(hit);
            return;
        }
        Move(Velocity);
        UpdateRotation();
    }

    //OnHitEntity damage is the speed at the moment of impact times the base damage, maps to vanilla onHitEntity
    protected override void OnHitEntity(ProjectileHitResult hit)
    {
        base.OnHitEntity(hit);
        //Vanilla arrows can pierce and stick into mobs; here it deals damage once and disappears
        var damage = (float)Mth.Clamp(Velocity.Length() * BaseDamage, 0.0, int.MaxValue);
        //The source is the arrow's own position, the target is knocked back along the flight direction
        hit.Entity?.Hurt((float)Math.Ceiling(damage), Pos);
        Discard();
    }
}

//Arrow plain arrow, maps to vanilla net.minecraft.world.entity.projectile.arrow.Arrow
//Spectral arrows and tridents are not implemented here, they differ only in appearance and effects
public sealed class Arrow : AbstractArrow
{
    public Arrow(EntityType<object> type) : base(type) { }
}
