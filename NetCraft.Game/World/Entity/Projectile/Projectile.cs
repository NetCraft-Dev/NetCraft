using NetCraft.Game.Server;
using NetCraft.Game.World.Level.Block;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
//方向同时存在于 Primitives 与 Registry.Enums 投射物几何一律用前者
using Direction = NetCraft.Primitives.Direction;
//Util 下另有 Random 命名空间直接 using 会与 System.Random 撞名 只取 Mth
using Mth = NetCraft.Util.Mth;

namespace NetCraft.Game.World.Entity;

//ProjectileHitType 投射物命中类型 对应原版 HitResult.Type
public enum ProjectileHitType
{
    miss,
    block,
    entity,
}

//ProjectileHitResult 投射物命中结果 对应原版 HitResult
//命中实体时 Entity 非空 未命中与命中方块时 Location 是移动终点
public readonly record struct ProjectileHitResult(
    ProjectileHitType Type,
    Vec3 Location,
    NetCraft.Registry.Entity? Entity,
    BlockPos BlockPos,
    Direction Direction)
{
    //Miss 未命中 位置即移动终点
    public static ProjectileHitResult Miss(Vec3 location)
        => new(ProjectileHitType.miss, location, null, BlockPos.Zero, Direction.Up);

    //BlockHit 命中方块但只有落点 方块坐标由落点推出来 对应没有精确射线时的兜底
    public static ProjectileHitResult BlockHit(Vec3 location)
        => new(ProjectileHitType.block, location, null,
            new BlockPos(Mth.Floor(location.X), Mth.Floor(location.Y), Mth.Floor(location.Z)), Direction.Up);

    //FromBlockHit 把方块射线结果转成命中结果 带回命中方块与进入面
    public static ProjectileHitResult FromBlockHit(BlockHitResult hit)
        => new(ProjectileHitType.block, hit.Location, null, hit.BlockPos, hit.Direction);

    //EntityHit 命中实体
    public static ProjectileHitResult EntityHit(Vec3 location, NetCraft.Registry.Entity entity)
        => new(ProjectileHitType.entity, location, entity, BlockPos.Zero, Direction.Up);

    //ToBlockHitResult 还原成射线命中结果 供方块回调使用
    public BlockHitResult ToBlockHitResult() => new(BlockPos, Direction, Location, false);
}

//ProjectileUtil 投射物命中检测工具 对应原版 net.minecraft.world.entity.projectile.ProjectileUtil
//方块命中走服务端方块射线取精确命中点 实体命中做移动线段与包围盒求交 两者取更近的
public static class ProjectileUtil
{
    //InflateAmount 实体命中判定的包边量 对应原版 0.3
    private const double InflateAmount = 0.3;

    //GetHitResult 取本刻移动线段上最近的命中 对应原版 getHitResultOnMoveVector
    public static ProjectileHitResult GetHitResult(Projectile projectile)
    {
        var from = projectile.Pos;
        var to = from.Add(projectile.Velocity);
        if (projectile.Level is not PersistentServerLevel level) return ProjectileHitResult.Miss(to);
        var blockResult = ServerBlockRaycast.Clip(level, from, to) is { } blockHit
            ? ProjectileHitResult.FromBlockHit(blockHit)
            : ProjectileHitResult.Miss(to);
        var entityResult = GetEntityHit(projectile);
        if (entityResult.Type != ProjectileHitType.entity) return blockResult;
        if (blockResult.Type != ProjectileHitType.block) return entityResult;
        return from.DistanceToSqr(entityResult.Location) <= from.DistanceToSqr(blockResult.Location)
            ? entityResult
            : blockResult;
    }

    //GetEntityHit 取移动线段上最近的实体命中 没有命中给 Miss(移动终点)
    public static ProjectileHitResult GetEntityHit(Projectile projectile)
    {
        var from = projectile.Pos;
        var to = from.Add(projectile.Velocity);
        if (projectile.Level is not PersistentServerLevel level) return ProjectileHitResult.Miss(to);
        //搜索范围按移动全程扫过的区域再外扩一格 对应原版 expandTowards(movement).inflate(1.0)
        var search = projectile.BoundingBox.ExpandTowards(projectile.Velocity).Inflate(1.0, 1.0, 1.0);
        var result = ProjectileHitResult.Miss(to);
        var closest = double.MaxValue;
        foreach (var candidate in level.EntitiesInBox(search))
        {
            if (!projectile.CanHitEntity(candidate)) continue;
            var box = candidate.BoundingBox.Inflate(InflateAmount, InflateAmount, InflateAmount);
            if (AABB.Clip(new[] { box }, from, to, BlockPos.Zero) is not { } hit) continue;
            var distance = from.DistanceToSqr(hit.Location);
            if (distance >= closest) continue;
            closest = distance;
            result = ProjectileHitResult.EntityHit(hit.Location, candidate);
        }
        return result;
    }
}

//Projectile 投射物基类 对应原版 net.minecraft.world.entity.projectile.Projectile
//持有发射者身份与射出方向计算 命中分发交给子类
//本作没有防御反弹(deflect)与穿透体系 相应分支不做
public abstract class Projectile : NetCraft.Registry.Entity
{
    private readonly EntityType<object> _type;
    //_leftOwner 是否已离开与发射者的重叠 重叠期间不与发射者碰撞 对应原版 leftOwner
    private bool _leftOwner;
    private bool _leftOwnerChecked;

    protected Projectile(EntityType<object> type) => _type = type;

    public override Identifier Id => _type.Id;

    public override EntityType<object>? Type => _type;

    //OwnerUuid 发射者 命中判定要放行发射者自己 对应原版 owner
    public Guid? OwnerUuid { get; private set; }

    //SetOwner 记录发射者
    public void SetOwner(Guid? ownerUuid) => OwnerUuid = ownerUuid;

    public void SetOwner(NetCraft.Registry.Entity? owner) => OwnerUuid = owner?.Uuid;

    //LeftOwner 是否已离开与发射者的重叠
    public bool LeftOwner => _leftOwner;

    //Shoot 按方向分量射出 对应原版 Projectile.shoot
    //pow 是初速大小 uncertainty 是散布
    public virtual void Shoot(double xd, double yd, double zd, float pow, float uncertainty)
    {
        var movement = GetMovementToShoot(xd, yd, zd, pow, uncertainty);
        Velocity = movement;
        var horizontal = Math.Sqrt(movement.X * movement.X + movement.Z * movement.Z);
        YRot = (float)(Math.Atan2(movement.X, movement.Z) * (180.0 / Math.PI));
        XRot = (float)(Math.Atan2(movement.Y, horizontal) * (180.0 / Math.PI));
    }

    //GetMovementToShoot 方向归一化后加三角分布散布再乘速度大小 对应原版同名方法
    public Vec3 GetMovementToShoot(double xd, double yd, double zd, float pow, float uncertainty)
    {
        var direction = new Vec3(xd, yd, zd).Normalize();
        var deviation = 0.0172275 * uncertainty;
        return direction
            .Add(Triangle(0.0, deviation), Triangle(0.0, deviation), Triangle(0.0, deviation))
            .Multiply(pow);
    }

    //CheckLeftOwner 确认是否已离开与发射者的重叠 对应原版 checkLeftOwner
    //只算一次 算过就不再复查 原版同样用 leftOwnerChecked 短路
    protected void CheckLeftOwner()
    {
        if (_leftOwner || _leftOwnerChecked) return;
        _leftOwnerChecked = true;
        _leftOwner = true;
        if (OwnerUuid is not { } ownerUuid) return;
        if (Level is not PersistentServerLevel level) return;
        var box = BoundingBox.ExpandTowards(Velocity).Inflate(1.0, 1.0, 1.0);
        foreach (var entity in level.EntitiesInBox(box))
        {
            if (entity.Uuid != ownerUuid) continue;
            _leftOwner = false;
            return;
        }
    }

    //CanHitEntity 该实体能否被本投射物命中 已移除的与还在发射者身上的发射者不算
    internal bool CanHitEntity(NetCraft.Registry.Entity entity)
    {
        if (entity.IsRemoved) return false;
        if (!_leftOwner && OwnerUuid is { } ownerUuid && entity.Uuid == ownerUuid) return false;
        return true;
    }

    //OnHit 命中分发 对应原版 Projectile.onHit
    //命中方块时先通知方块自身 标靶这类方块靠命中点算输出强度
    protected virtual void OnHit(ProjectileHitResult hit)
    {
        switch (hit.Type)
        {
            case ProjectileHitType.entity:
                OnHitEntity(hit);
                break;
            case ProjectileHitType.block:
                NotifyBlockHit(hit);
                OnHitBlock(hit);
                break;
        }
    }

    //NotifyBlockHit 通知被命中的方块 对应原版 BlockState.onProjectileHit
    private void NotifyBlockHit(ProjectileHitResult hit)
    {
        if (Level is not PersistentServerLevel level) return;
        if (level.GetBlockState(hit.BlockPos) is not { } state) return;
        if (state.Owner is not BlockBehaviour behaviour) return;
        behaviour.OnProjectileHit(level, state, hit.ToBlockHitResult(), this);
    }

    protected virtual void OnHitEntity(ProjectileHitResult hit) { }

    protected virtual void OnHitBlock(ProjectileHitResult hit) { }

    //ApplyGravityAndInertia 先加重力再按空气阻力衰减 对应原版 applyGravity 后 applyInertia
    protected void ApplyGravityAndInertia()
        => Velocity = Velocity.Add(0.0, -DefaultGravity, 0.0).Multiply(AirDrag);

    //UpdateRotation 朝向跟随速度 对应原版 Projectile.updateRotation
    protected void UpdateRotation()
    {
        var movement = Velocity;
        var horizontal = Math.Sqrt(movement.X * movement.X + movement.Z * movement.Z);
        XRot = LerpRotation(XRot, (float)(Math.Atan2(movement.Y, horizontal) * (180.0 / Math.PI)));
        YRot = LerpRotation(YRot, (float)(Math.Atan2(movement.X, movement.Z) * (180.0 / Math.PI)));
    }

    //AddAdditionalSaveData 只存发射者与是否已离手 位置速度由基类统一写
    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        if (OwnerUuid is { } ownerUuid) tag.PutIntArray("Owner", UuidToIntArray(ownerUuid));
        if (_leftOwner) tag.PutBoolean("LeftOwner", true);
    }

    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        if (tag.GetIntArray("Owner") is { } owner) OwnerUuid = IntArrayToUuid(owner.Value);
        _leftOwner = tag.GetBooleanOr("LeftOwner", false);
    }

    //Triangle 三角分布随机 对应原版 RandomSource.triangle 两个均匀分布相减
    private static double Triangle(double mean, double deviation)
        => mean + deviation * (Random.Shared.NextDouble() - Random.Shared.NextDouble());

    //LerpRotation 朝向插值前先把差值折进正负 180 度 对应原版 Projectile.lerpRotation
    private static float LerpRotation(float rotO, float rot)
    {
        while (rot - rotO < -180f) rotO -= 360f;
        while (rot - rotO >= 180f) rotO += 360f;
        return (float)(rotO + 0.2 * (rot - rotO));
    }
}
