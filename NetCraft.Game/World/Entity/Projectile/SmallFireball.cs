using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Entity;

//AbstractHurtingProjectile 火球类投射物基类 对应原版 net.minecraft.world.entity.projectile.hurtingprojectile.AbstractHurtingProjectile
//这类投射物不受重力 只按阻力减速 飞行途中会引燃碰到的东西
public abstract class AbstractHurtingProjectile : Projectile
{
    protected AbstractHurtingProjectile(EntityType<object> type) : base(type) { }

    //DefaultGravity 火球不受重力 对应原版 AbstractHurtingProjectile 不加重力
    public override double DefaultGravity => 0.0;

    //AirDrag 火球空气阻力 0.95 对应原版 0.95
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

//SmallFireball 小火球 对应原版 net.minecraft.world.entity.projectile.hurtingprojectile.SmallFireball
//火焰弹投出去就是它 命中生物造成 5 点伤害 命中方块在空气处点一把火
//原版还会给生物附加 5 秒着火 本作没有着火刻数体系故只造成伤害
public sealed class SmallFireball : AbstractHurtingProjectile
{
    //FireDamage 命中生物的伤害 对应原版 5.0f
    private const float FireDamage = 5f;

    public SmallFireball(EntityType<object> type) : base(type) { }

    protected override void OnHitEntity(ProjectileHitResult hit)
    {
        base.OnHitEntity(hit);
        //以火球自身位置为来源 命中目标会被沿飞行方向击退
        hit.Entity?.Hurt(FireDamage, Pos);
        Discard();
    }

    protected override void OnHitBlock(ProjectileHitResult hit)
    {
        base.OnHitBlock(hit);
        PlaceFire(hit);
        Discard();
    }

    //PlaceFire 命中面的外侧格是空气就点一把火 对应原版命中方块后的引燃
    private void PlaceFire(ProjectileHitResult hit)
    {
        if (Level is not PersistentServerLevel level) return;
        var pos = hit.BlockPos.Relative(hit.Direction, 1);
        if (level.GetBlockState(pos) is not { } state) return;
        if (!ReferenceEquals(state.Owner, Blocks.AIR)) return;
        level.SetBlock(pos, Blocks.FIRE.DefaultBlockState);
    }
}
