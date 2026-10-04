using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Level.Block.Dispenser;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Items;

//DispenseConfig 发射器的发射参数 对应原版 net.minecraft.world.item.ProjectileItem.DispenseConfig
//落点函数 散布 初速 以及覆盖默认发射音效的事件号
public sealed record DispenseConfig(
    Func<BlockSource, NetCraft.Primitives.Direction, Vec3> PositionFunction,
    float Uncertainty,
    float Power,
    int? OverrideDispenseEvent)
{
    //Default 默认发射参数 落点在中点前方 0.7 格再抬高 0.1 散布 6 初速 1.1 对应原版 DispenseConfig.DEFAULT
    public static readonly DispenseConfig Default = new(
        (source, direction) => Blocks.DispenserBlock.GetDispensePosition(source, 0.7, new Vec3(0.0, 0.1, 0.0)),
        6.0f,
        1.1f,
        null);
}

//ProjectileItem 能作为投射物射出去的物品 对应原版 net.minecraft.world.item.ProjectileItem
//发射器按它造实体 玩家投掷将来也走同一条路
public interface ProjectileItem
{
    //AsProjectile 造出待发射的投射物实体并填好位置与携带物品 对应原版 asProjectile
    Projectile AsProjectile(Vec3 position, ItemStack stack);

    //CreateDispenseConfig 发射参数 对应原版 createDispenseConfig
    DispenseConfig CreateDispenseConfig() => DispenseConfig.Default;

    //Shoot 让投射物把方向分量转成速度 对应原版 shoot
    void Shoot(Projectile projectile, double xd, double yd, double zd, float pow, float uncertainty)
        => projectile.Shoot(xd, yd, zd, pow, uncertainty);

    //Use 玩家手持本物品右键投掷 对应原版雪球那类物品的 use
    void Use(PersistentServerLevel level, ServerPlayer player, ItemStack stack);
}

//ProjectileItemBase 投射物物品基类 统一注册名与实体构造
public abstract class ProjectileItemBase : Item, ProjectileItem
{
    private readonly string _name;

    protected ProjectileItemBase(string name) => _name = name;

    public override Identifier Id => Identifier.WithDefaultNamespace(_name);

    //AsProjectile 造实体并填位置与携带物品 发射器发射的投射物没有发射者 与原版一致
    public Projectile AsProjectile(Vec3 position, ItemStack stack)
    {
        var projectile = CreateProjectile();
        projectile.Pos = position;
        if (projectile is ThrowableItemProjectile itemProjectile) itemProjectile.Item = stack;
        return projectile;
    }

    public virtual DispenseConfig CreateDispenseConfig() => DispenseConfig.Default;

    //ThrowPower 玩家手投的初速 对应原版 PROJECTILE_SHOOT_POWER 1.5
    protected virtual float ThrowPower => 1.5f;

    //ThrowUncertainty 玩家手投的散布 对应原版 1.0
    protected virtual float ThrowUncertainty => 1.0f;

    //Use 在眼睛下方 0.1 格造投射物朝视线方向射出 消耗与同步交给调用方
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

    //Shoot 实现 ProjectileItem.Shoot 让类内也能直接调到
    public virtual void Shoot(Projectile projectile, double xd, double yd, double zd, float pow,
        float uncertainty)
        => projectile.Shoot(xd, yd, zd, pow, uncertainty);

    //LookVector 由朝向算视线单位向量 对应原版 Entity.shootFromRotation 开头的三角函数
    private static (double X, double Y, double Z) LookVector(float yaw, float pitch)
    {
        var yawRad = yaw * 0.017453292f;
        var pitchRad = pitch * 0.017453292f;
        return (-Mth.Sin(yawRad) * Mth.Cos(pitchRad),
            -Mth.Sin(pitchRad),
            Mth.Cos(yawRad) * Mth.Cos(pitchRad));
    }

    //CreateProjectile 造出具体投射物实体 实体类型由子类绑定
    protected abstract Projectile CreateProjectile();
}

//ArrowItem 箭 对应原版 ArrowItem
public sealed class ArrowItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new Arrow(EntityTypes.ARROW);
}

//SnowballItem 雪球 对应原版 SnowballItem
public sealed class SnowballItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new Snowball(EntityTypes.SNOWBALL);
}

//EggItem 鸡蛋 对应原版 EggItem
public sealed class EggItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new ThrownEgg(EntityTypes.EGG);
}

//EnderPearlItem 末影珍珠 对应原版 EnderpearlItem
public sealed class EnderPearlItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new ThrownEnderpearl(EntityTypes.ENDER_PEARL);
}

//FireChargeItem 火焰弹 对应原版 FireChargeItem 的投射物部分
public sealed class FireChargeItem(string name) : ProjectileItemBase(name)
{
    protected override Projectile CreateProjectile() => new SmallFireball(EntityTypes.SMALL_FIREBALL);

    //火焰弹落点更远初速更小 音效换成放火声 对应原版 createDispenseConfig
    public override DispenseConfig CreateDispenseConfig() => new(
        (source, direction) => Blocks.DispenserBlock.GetDispensePosition(source, 1.0, Vec3.Zero),
        6.6666665f,
        1.0f,
        1018);
}
