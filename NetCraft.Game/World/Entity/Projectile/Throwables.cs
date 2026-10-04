using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Entity;

//Snowball 雪球 对应原版 net.minecraft.world.entity.projectile.throwableitemprojectile.Snowball
//命中即碎 对普通生物没有伤害 原版只对烈焰人造成 3 点 本作没有烈焰人故伤害恒为零
public sealed class Snowball : ThrowableItemProjectile
{
    public Snowball(EntityType<object> type) : base(type) { }

    protected override void OnHit(ProjectileHitResult hit)
    {
        base.OnHit(hit);
        Discard();
    }
}

//ThrownEgg 鸡蛋 对应原版 net.minecraft.world.entity.projectile.throwableitemprojectile.ThrownEgg
//命中后有小概率孵出小鸡
public sealed class ThrownEgg : ThrowableItemProjectile
{
    //HatchChance 命中后孵出小鸡的倒数概率 对应原版 1/8
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

//ThrownEnderpearl 末影珍珠 对应原版 net.minecraft.world.entity.projectile.throwableitemprojectile.ThrownEnderpearl
//命中后把发射者挪到落点 原版还给发射者 5 点伤害与区块加载票据 后者本作没有
//玩家不在关卡实体集合里 手投的末影珍珠暂时找不到发射者 等玩家实体接入再补
public sealed class ThrownEnderpearl : ThrowableItemProjectile
{
    public ThrownEnderpearl(EntityType<object> type) : base(type) { }

    protected override void OnHit(ProjectileHitResult hit)
    {
        base.OnHit(hit);
        //落点取移动前的位置 避免把发射者塞进命中点所在方块里 对应原版 oldPosition
        var teleportPos = PreviousPos;
        if (Level is PersistentServerLevel level && FindOwner(level) is { } owner)
        {
            owner.Pos = teleportPos;
            owner.Velocity = Vec3.Zero;
        }
        Discard();
    }

    //FindOwner 在关卡实体集合里按 uuid 找回发射者
    private NetCraft.Registry.Entity? FindOwner(PersistentServerLevel level)
    {
        if (OwnerUuid is not { } uuid) return null;
        foreach (var entity in level.Entities)
            if (entity.Uuid == uuid) return entity;
        return null;
    }
}
