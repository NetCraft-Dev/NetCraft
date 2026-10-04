using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Entity;

//AbstractArrow 箭类投射物基类 对应原版 net.minecraft.world.entity.projectile.arrow.AbstractArrow
//重力 0.05 空气阻力 0.99 命中方块后插在原地 过一段时间自然消失
//原版还有拾取(玩家能捡回箭)与穿透附魔 本作没有玩家物品使用体系 只保留插地与消失
public abstract class AbstractArrow : Projectile
{
    //BaseDamage 箭的基础伤害 对应原版默认 2.0 最终伤害再乘飞行速度
    private const double BaseDamage = 2.0;

    //AirborneDespawnTicks 插地后的存活刻数 对应原版 1200 刻(60 秒)
    private const int AirborneDespawnTicks = 1200;

    private bool _inGround;
    private int _inGroundTime;

    protected AbstractArrow(EntityType<object> type) : base(type) { }

    //DefaultGravity 箭重力 0.05 对应原版 getDefaultGravity
    public override double DefaultGravity => 0.05;

    //AirDrag 箭空气阻力 0.99 对应原版 getAirDrag
    public override double AirDrag => 0.99;

    //InGround 箭是否已插在方块上
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
            //撞上方块就插住 位置停在命中点 对应原版命中方块的处理
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

    //OnHitEntity 伤害按命中瞬间的速度乘基础伤害 对应原版 onHitEntity
    protected override void OnHitEntity(ProjectileHitResult hit)
    {
        base.OnHitEntity(hit);
        //原版箭还能穿透与插在生物身上 本作只造成一次伤害后消失
        var damage = (float)Mth.Clamp(Velocity.Length() * BaseDamage, 0.0, int.MaxValue);
        //以箭自身位置为来源 命中目标会被沿飞行方向击退
        hit.Entity?.Hurt((float)Math.Ceiling(damage), Pos);
        Discard();
    }
}

//Arrow 普通箭 对应原版 net.minecraft.world.entity.projectile.arrow.Arrow
//发光箭与三叉戟本作未实现 行为差异只在外观与效果
public sealed class Arrow : AbstractArrow
{
    public Arrow(EntityType<object> type) : base(type) { }
}
