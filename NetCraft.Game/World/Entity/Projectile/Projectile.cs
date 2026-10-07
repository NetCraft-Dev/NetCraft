using NetCraft.Game.Server;
using NetCraft.Game.World.Level.Block;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
//Direction exists in both Primitives and Registry.Enums, projectile geometry always uses the former
using Direction = NetCraft.Primitives.Direction;
//Util also has a Random namespace and using it directly would clash with System.Random, so only Mth is imported
using Mth = NetCraft.Util.Mth;

namespace NetCraft.Game.World.Entity;

//ProjectileHitType projectile hit type, maps to vanilla HitResult.Type
public enum ProjectileHitType
{
    miss,
    block,
    entity,
}

//ProjectileHitResult projectile hit result, maps to vanilla HitResult
//Entity is non-null on an entity hit; on a miss or block hit Location is the end of the movement
public readonly record struct ProjectileHitResult(
    ProjectileHitType Type,
    Vec3 Location,
    NetCraft.Registry.Entity? Entity,
    BlockPos BlockPos,
    Direction Direction)
{
    //Miss no hit, the position is the end of the movement
    public static ProjectileHitResult Miss(Vec3 location)
        => new(ProjectileHitType.miss, location, null, BlockPos.Zero, Direction.Up);

    //BlockHit block hit with only the hit point, the block position is derived from it, a fallback when there is no precise ray
    public static ProjectileHitResult BlockHit(Vec3 location)
        => new(ProjectileHitType.block, location, null,
            new BlockPos(Mth.Floor(location.X), Mth.Floor(location.Y), Mth.Floor(location.Z)), Direction.Up);

    //FromBlockHit converts a block ray result into a hit result, carrying the hit block and entered face
    public static ProjectileHitResult FromBlockHit(BlockHitResult hit)
        => new(ProjectileHitType.block, hit.Location, null, hit.BlockPos, hit.Direction);

    //EntityHit entity hit
    public static ProjectileHitResult EntityHit(Vec3 location, NetCraft.Registry.Entity entity)
        => new(ProjectileHitType.entity, location, entity, BlockPos.Zero, Direction.Up);

    //ToBlockHitResult restores it into a ray hit result for block callbacks
    public BlockHitResult ToBlockHitResult() => new(BlockPos, Direction, Location, false);
}

//ProjectileUtil projectile hit detection utilities, maps to vanilla net.minecraft.world.entity.projectile.ProjectileUtil
//Block hits use a server-side block ray for the precise hit point and entity hits intersect the movement segment with bounding boxes, the nearer one wins
public static class ProjectileUtil
{
    //InflateAmount bounding box inflation for entity hit tests, maps to vanilla 0.3
    private const double InflateAmount = 0.3;

    //GetHitResult returns the nearest hit along this tick's movement segment, maps to vanilla getHitResultOnMoveVector
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

    //GetEntityHit returns the nearest entity hit on the movement segment, a miss gives Miss (end of movement)
    public static ProjectileHitResult GetEntityHit(Projectile projectile)
    {
        var from = projectile.Pos;
        var to = from.Add(projectile.Velocity);
        if (projectile.Level is not PersistentServerLevel level) return ProjectileHitResult.Miss(to);
        //The search area is the region swept by the whole movement expanded by one block, maps to vanilla expandTowards(movement).inflate(1.0)
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

//Projectile projectile base class, maps to vanilla net.minecraft.world.entity.projectile.Projectile
//Holds the owner identity and the launch direction computation, hit dispatch is left to subclasses
//This project has no deflect or piercing system, the corresponding branches are skipped
public abstract class Projectile : NetCraft.Registry.Entity
{
    private readonly EntityType<object> _type;
    //_leftOwner whether it has left the overlap with its owner, it does not collide with the owner while overlapping, maps to vanilla leftOwner
    private bool _leftOwner;
    private bool _leftOwnerChecked;

    protected Projectile(EntityType<object> type) => _type = type;

    public override Identifier Id => _type.Id;

    public override EntityType<object>? Type => _type;

    //OwnerUuid the owner, hit tests must let the owner through, maps to vanilla owner
    public Guid? OwnerUuid { get; private set; }

    //SetOwner records the owner
    public void SetOwner(Guid? ownerUuid) => OwnerUuid = ownerUuid;

    public void SetOwner(NetCraft.Registry.Entity? owner) => OwnerUuid = owner?.Uuid;

    //LeftOwner whether it has left the overlap with its owner
    public bool LeftOwner => _leftOwner;

    //Shoot shoots along the direction components, maps to vanilla Projectile.shoot
    //pow is the initial speed and uncertainty is the spread
    public virtual void Shoot(double xd, double yd, double zd, float pow, float uncertainty)
    {
        var movement = GetMovementToShoot(xd, yd, zd, pow, uncertainty);
        Velocity = movement;
        var horizontal = Math.Sqrt(movement.X * movement.X + movement.Z * movement.Z);
        YRot = (float)(Math.Atan2(movement.X, movement.Z) * (180.0 / Math.PI));
        XRot = (float)(Math.Atan2(movement.Y, horizontal) * (180.0 / Math.PI));
    }

    //GetMovementToShoot normalizes the direction, adds triangle-distributed spread and scales by the speed, maps to the vanilla method of the same name
    public Vec3 GetMovementToShoot(double xd, double yd, double zd, float pow, float uncertainty)
    {
        var direction = new Vec3(xd, yd, zd).Normalize();
        var deviation = 0.0172275 * uncertainty;
        return direction
            .Add(Triangle(0.0, deviation), Triangle(0.0, deviation), Triangle(0.0, deviation))
            .Multiply(pow);
    }

    //CheckLeftOwner confirms whether it has left the overlap with its owner, maps to vanilla checkLeftOwner
    //Computed only once and never rechecked, vanilla also short-circuits with leftOwnerChecked
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

    //CanHitEntity whether the entity can be hit by this projectile; removed ones and the owner while still on it do not count
    internal bool CanHitEntity(NetCraft.Registry.Entity entity)
    {
        if (entity.IsRemoved) return false;
        if (!_leftOwner && OwnerUuid is { } ownerUuid && entity.Uuid == ownerUuid) return false;
        return true;
    }

    //OnHit hit dispatch, maps to vanilla Projectile.onHit
    //On a block hit the block itself is notified first; blocks like targets use the hit point to compute the output strength
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

    //NotifyBlockHit notifies the hit block, maps to vanilla BlockState.onProjectileHit
    private void NotifyBlockHit(ProjectileHitResult hit)
    {
        if (Level is not PersistentServerLevel level) return;
        if (level.GetBlockState(hit.BlockPos) is not { } state) return;
        if (state.Owner is not BlockBehaviour behaviour) return;
        behaviour.OnProjectileHit(level, state, hit.ToBlockHitResult(), this);
    }

    protected virtual void OnHitEntity(ProjectileHitResult hit) { }

    protected virtual void OnHitBlock(ProjectileHitResult hit) { }

    //ApplyGravityAndInertia adds gravity then dampens by air drag, maps to vanilla applyGravity followed by applyInertia
    protected void ApplyGravityAndInertia()
        => Velocity = Velocity.Add(0.0, -DefaultGravity, 0.0).Multiply(AirDrag);

    //UpdateRotation makes the facing follow the velocity, maps to vanilla Projectile.updateRotation
    protected void UpdateRotation()
    {
        var movement = Velocity;
        var horizontal = Math.Sqrt(movement.X * movement.X + movement.Z * movement.Z);
        XRot = LerpRotation(XRot, (float)(Math.Atan2(movement.Y, horizontal) * (180.0 / Math.PI)));
        YRot = LerpRotation(YRot, (float)(Math.Atan2(movement.X, movement.Z) * (180.0 / Math.PI)));
    }

    //AddAdditionalSaveData stores only the owner and whether it has left the hand, position and velocity are written by the base class
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

    //Triangle triangle-distributed random, maps to vanilla RandomSource.triangle, the difference of two uniform distributions
    private static double Triangle(double mean, double deviation)
        => mean + deviation * (Random.Shared.NextDouble() - Random.Shared.NextDouble());

    //LerpRotation folds the delta into plus or minus 180 degrees before interpolating the facing, maps to vanilla Projectile.lerpRotation
    private static float LerpRotation(float rotO, float rot)
    {
        while (rot - rotO < -180f) rotO -= 360f;
        while (rot - rotO >= 180f) rotO += 360f;
        return (float)(rotO + 0.2 * (rot - rotO));
    }
}
