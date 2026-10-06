using System.Buffers.Binary;
using System.Threading;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Primitives.Phys;
using NetCraft.Util;
using NetCraft.Util.Random;
//Entity attributes carry their own namespace, kept separate from same-named types in environment attributes (NetCraft.Registry.Environment)
//Attribute clashes with System.Attribute, so only the three needed names are taken and aliased here
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;

namespace NetCraft.Registry;

//ITrackedEntity the entity view needed by the entity tracking layer
//Both server entities and the player object managing entity data implement it; the tracker depends only on this set of read/write capabilities
public interface ITrackedEntity
{
    //EntityId entity network id
    int EntityId { get; }

    //Type entity type; null means tracking is not supported
    EntityType<object>? Type { get; }

    //Uuid entity unique identifier
    Guid Uuid { get; }

    //Pos entity position
    Vec3 Pos { get; }

    //Velocity entity velocity
    Vec3 Velocity { get; }

    //YRot yaw
    float YRot { get; }

    //XRot pitch
    float XRot { get; }

    //OnGround whether it is touching the ground
    bool OnGround { get; }

    //Attributes entity attribute map; entities without an attribute system return null and the tracking layer skips attribute sync
    AttributeMap? Attributes { get; }
}

//Entity abstract base class, maps to vanilla net.minecraft.world.entity.Entity
//Holds core fields EntityId/Pos/Uuid/Velocity/YRot/XRot; Level uses object as a placeholder until the Level subsystem is ready
//Vanilla holds a CompoundTag persistence field, simplified here so subclasses extend as needed
public abstract class Entity : ITrackedEntity, ISyncedEntity
{
    //_entityCounter global entity id counter, maps to vanilla ServerLevel.ENTITY_COUNTER
    //Placed here rather than on the level so entities and players share one id space; two counters would collide
    private static int _entityCounter;

    //SharedFlagsIndex shared flags index, maps to vanilla Entity.DATA_SHARED_FLAGS_ID
    public const byte SharedFlagsIndex = 0;

    //PoseIndex pose index, maps to vanilla Entity.DATA_POSE
    public const byte PoseIndex = 6;

    //SyncedData entity metadata container; subclasses Define the entries they own at construction
    public SynchedEntityData SyncedData { get; } = new();

    //_deathHandled whether the death hook already fired, ensuring the death flow runs only once
    private bool _deathHandled;

    //Gravity gravitational acceleration, maps to vanilla 0.08
    public const double Gravity = 0.08;

    //VerticalDrag vertical drag, maps to vanilla 0.98
    public const double VerticalDrag = 0.98;

    //HorizontalDrag horizontal drag, maps to vanilla 0.91
    public const double HorizontalDrag = 0.91;

    //DefaultGravity default gravitational acceleration, maps to vanilla getDefaultGravity; dropped items override it to 0.04
    public virtual double DefaultGravity => Gravity;

    //SafeFallDistance the distance at which fall damage starts counting, maps to vanilla attribute SAFE_FALL_DISTANCE, default 3
    public virtual double SafeFallDistance => 3.0;

    //FallDamageMultiplier fall damage multiplier, maps to vanilla attribute FALL_DAMAGE_MULTIPLIER, default 1
    public virtual double FallDamageMultiplier => 1.0;

    //KnockbackResistance knockback resistance between 0 and 1, where 1 means full immunity, maps to vanilla attribute KNOCKBACK_RESISTANCE
    public virtual double KnockbackResistance => 0.0;

    //TakesFallDamage whether it takes fall damage; in vanilla the Entity base does not and LivingEntity does
    //There is no LivingEntity layer here, so living subclasses turn it on
    public virtual bool TakesFallDamage => false;

    //IsInWater whether it is submerged; there is no fluid check here so it is always false, maps to vanilla wasTouchingWater
    //Until the fluid check is wired up this lets all falls accumulate distance, matching vanilla on-land behavior
    public virtual bool IsInWater => false;

    //KnockbackDirectionEpsilon minimum squared length of the knockback direction, maps to vanilla 9.999999747378752E-6
    //When the direction component is shorter than this it is randomized, so a purely vertical knockback does not pin the entity in place
    private const double KnockbackDirectionEpsilon = 9.999999747378752E-6;

    //_random the entity's own random source, maps to vanilla Entity.random, used for knockback direction randomization and the like
    private readonly RandomSource _random = RandomSource.Create();

    //FallDistance fall distance accumulated before this tick, maps to vanilla fallDistance; cleared on landing or touching a reset surface
    public double FallDistance { get; private set; }

    //AirDrag air drag, maps to vanilla getAirDrag, default 0.98
    public virtual double AirDrag => VerticalDrag;

    //SavesHealth whether the base class writes the Health field
    //A dropped item's Health is a separate short field with the same name as a mob's; it overrides this to false and writes it itself
    protected virtual bool SavesHealth => true;

    //TagsTag key name for entity tags in saves, maps to vanilla "Tags"
    private const string TagsTag = "Tags";

    //MaxTagCount maximum tag count per entity, maps to vanilla 1024; adding beyond a full set fails
    private const int MaxTagCount = 1024;

    //_tags custom string tag set of the entity, maps to vanilla Entity.tags
    private readonly HashSet<string> _tags = new();

    //GetTags gets all tags, maps to vanilla entityTags
    public IReadOnlyCollection<string> GetTags() => _tags;

    //AddTag adds a tag, returning false if it exists or the limit is reached, maps to vanilla addTag
    public bool AddTag(string tag)
    {
        if (_tags.Count >= MaxTagCount) return false;
        return _tags.Add(tag);
    }

    //RemoveTag removes a tag, returning false if absent, maps to vanilla removeTag
    public bool RemoveTag(string tag) => _tags.Remove(tag);

    //HasTag whether the entity has the tag, used by selector-style checks
    public bool HasTag(string tag) => _tags.Contains(tag);

    //EntityId entity network id, assigned by the level when joining; 0 before joining
    //Vanilla sets 0 in the constructor then calls level.getNextEntityId(); here the level reference is injected after construction so assignment is deferred too
    public int EntityId { get; private set; }

    //SetId directly sets the entity id, maps to vanilla Entity.setId
    public void SetId(int id) => EntityId = id;

    //NextEntityId gets the next available entity id, maps to vanilla ServerLevel.getNextEntityId
    //Starts at 0 and skips both 0 and taken ids; isTaken answers whether an id is already used
    //The caller must immediately register the assigned id in the taken set, otherwise the next assignment returns the same value
    public static int NextEntityId(Func<int, bool> isTaken)
    {
        var candidate = 0;
        while (true)
        {
            if (candidate != 0 && !isTaken(candidate)) return candidate;
            candidate = Interlocked.Increment(ref _entityCounter);
        }
    }

    //Id the entity's registry name, must be implemented by subclasses
    public abstract Identifier Id { get; }

    //Type entity type; subclasses override as needed and the tracking layer skips the entity when not overridden
    public virtual EntityType<object>? Type => null;

    //Level placeholder reference to the entity's world, to be replaced with a strong type once the Level subsystem is ready
    public object? Level { get; set; }

    //CollisionShapes collision shape query within the test area, injected by the level; when not injected there is no collision and only position advances
    //Vanilla has three pieces here: entity collision, world border and block collision on the level; the level supplies all of them at once
    public Func<AABB, IReadOnlyList<VoxelShape>>? CollisionShapes { get; set; }

    //NoPhysics whether to ignore collision and advance directly, maps to vanilla noPhysics
    public bool NoPhysics { get; set; }

    //HorizontalCollision whether either horizontal axis was blocked this tick, maps to vanilla horizontalCollision
    public bool HorizontalCollision { get; private set; }

    //VerticalCollision whether the vertical direction was blocked this tick, maps to vanilla verticalCollision
    public bool VerticalCollision { get; private set; }

    //VerticalCollisionBelow whether downward movement was blocked this tick; standing is judged by this rather than vertical collision, maps to vanilla verticalCollisionBelow
    public bool VerticalCollisionBelow { get; private set; }

    //Attributes entity attribute map; the base defaults to an empty table and living subclasses swap in their type's default table in the constructor
    //Maps to the AttributeMap held by vanilla LivingEntity
    public AttributeMap Attributes { get; protected set; } = new(AttributeSupplier.Empty);

    //GetAttributeValue gets the attribute's final value, maps to vanilla getAttributeValue
    public double GetAttributeValue(AttributeDef attribute) => Attributes.GetValue(attribute);

    //MaxUpStep the maximum step height that can be climbed automatically; the base is 0 and mobs override it from the step height attribute, maps to vanilla maxUpStep
    public virtual double MaxUpStep => 0.0;

    //Pos entity position in the world, defaulting to the origin
    public Vec3 Pos { get; set; } = Vec3.Zero;

    //Velocity entity velocity vector, defaulting to zero
    public Vec3 Velocity { get; set; } = Vec3.Zero;

    //Uuid entity unique identifier, randomly generated by default
    public Guid Uuid { get; set; } = Guid.NewGuid();

    //YRot/Yaw yaw, default 0
    public float YRot { get; set; }

    //XRot/Pitch pitch, default 0
    public float XRot { get; set; }

    //OnGround whether it is touching the ground, default false
    public bool OnGround { get; set; }

    //IsOnFire whether it is on fire, maps to vanilla isOnFire
    public bool IsOnFire { get; set; }

    //IsCrouching whether it is sneaking, maps to vanilla isCrouching
    public bool IsCrouching { get; set; }

    //IsSprinting whether it is sprinting, maps to vanilla isSprinting
    public bool IsSprinting { get; set; }

    //IsSwimming whether it is in the swimming pose, maps to vanilla isSwimming
    public bool IsSwimming { get; set; }

    //IsBaby whether it is a baby, maps to vanilla LivingEntity.isBaby
    public bool IsBaby { get; set; }

    //IsFallFlying whether it is elytra gliding, maps to vanilla LivingEntity.isFallFlying
    public bool IsFallFlying { get; set; }

    //IsFlying whether it is flying, maps to vanilla player ability flying and applies to all entities here
    public bool IsFlying { get; set; }

    //IsDescending whether it is in a descending pose, maps to vanilla isDescending
    //The collision context uses it to relax the lateral check for blocks like scaffolding; base entities always return false
    public virtual bool IsDescending() => false;

    //TickCount ticks the entity has lived, maps to vanilla tickCount; dropped items use it to decide merge frequency and despawn
    public int TickCount { get; private set; }

    //PreviousPos position before this tick, maps to vanilla xo/yo/zo, used to tell whether a block cell was crossed this tick
    public Vec3 PreviousPos { get; private set; } = Vec3.Zero;

    //MovementEpsilon displacement threshold; below it the entity is considered still, maps to vanilla 1.0E-7
    private const double MovementEpsilon = 1e-7;

    //Width hitbox width from the entity type's declared size, defaulting to the player size when no type is bound
    public double Width => Type?.Width ?? 0.6f;

    //Height hitbox height from the entity type's declared size, defaulting to the player size when no type is bound
    public double Height => Type?.Height ?? 1.8f;

    //BoundingBox current bounding box, maps to vanilla getBoundingBox
    public AABB BoundingBox => MakeBoundingBox(Pos);

    //MakeBoundingBox builds the bounding box from the foot position, half the width to each side around the feet, maps to vanilla makeBoundingBox
    public AABB MakeBoundingBox(Vec3 pos)
    {
        var half = Width / 2.0;
        return new AABB(pos.X - half, pos.Y, pos.Z - half, pos.X + half, pos.Y + Height, pos.Z + half);
    }

    //GetOnPos supporting block position below the feet, maps to vanilla getOnPos, default offset 0.2
    public BlockPos GetOnPos() => GetOnPos(0.2f);

    //GetOnPos takes the cell below by the given offset, maps to vanilla getOnPos(float)
    public BlockPos GetOnPos(float yOffset)
        => new(Mth.Floor(Pos.X), Mth.Floor(Pos.Y - yOffset), Mth.Floor(Pos.Z));

    //GetBlockPosBelowThatAffectsMyMovement the block below the feet that affects movement, maps to vanilla method of the same name
    //Vanilla uses an offset of 0.500001
    public BlockPos GetBlockPosBelowThatAffectsMyMovement() => GetOnPos(0.500001f);

    //Health current health, default 20 matching the vanilla MAX_HEALTH default
    public float Health { get; private set; } = 20f;

    //MaxHealth maximum health, overridden by subclasses as needed
    public float MaxHealth { get; protected set; } = 20f;

    //InvulnerableTime remaining invulnerability ticks after being hurt, maps to vanilla invulnerableTime
    public int InvulnerableTime { get; private set; }

    //IsDeadOrDying whether health dropped to zero, maps to vanilla isDeadOrDying
    public bool IsDeadOrDying => Health <= 0f;

    //Hurt applies damage, a minimal subset of vanilla hurtServer
    //No repeated damage during invulnerability; damage reduction, damage source types and the death animation phase are not implemented
    //When a knockback source position is given it also applies knockback, with the direction pointing from the source to itself
    public virtual bool Hurt(float amount, Vec3? knockbackSource = null)
    {
        if (IsDeadOrDying || InvulnerableTime > 0) return false;
        Health = Math.Max(0f, Health - amount);
        //Invulnerability for 10 ticks; vanilla is 20 ticks with 10 for the hurt animation
        InvulnerableTime = 10;
        //Hurt knockback with strength 0.4, maps to vanilla LivingEntity.dealDefaultKnockback
        //The direction is "source minus self" as in vanilla and knockback negates it internally; the net effect pushes the target away from the source
        if (knockbackSource is { } source)
            ApplyKnockback(0.4, source.X - Pos.X, source.Z - Pos.Z);
        if (Health <= 0f) TriggerDeath();
        return true;
    }

    //SetHealth directly sets health clamped to 0..MaxHealth, maps to vanilla setHealth
    public void SetHealth(float value)
    {
        Health = Math.Clamp(value, 0f, MaxHealth);
        //Health returning to a positive value counts as revival and re-allows the death flow
        if (Health > 0f)
        {
            _deathHandled = false;
            return;
        }
        TriggerDeath();
    }

    //Die hook when health reaches zero, maps to vanilla LivingEntity.die; subclasses do drops and death effects
    protected virtual void Die() { }

    //Died death event, hooked up by the level when the entity joins, for the server to broadcast the death effect and remove the entity
    public event Action<Entity>? Died;

    //TriggerDeath fires the death hook only once, avoiding Hurt and SetHealth running the death flow twice
    private void TriggerDeath()
    {
        if (_deathHandled) return;
        _deathHandled = true;
        Die();
        Died?.Invoke(this);
    }

    //Save writes the full entity save including the type id, maps to vanilla Entity.save
    public void Save(CompoundTag tag)
    {
        tag.PutString("id", (Type?.Id ?? Id).ToString());
        SaveWithoutId(tag);
    }

    //SaveWithoutId writes entity state without the type id, maps to vanilla Entity.saveWithoutId
    //Subclasses extending persistence fields should override AddAdditionalSaveData
    public virtual void SaveWithoutId(CompoundTag tag)
    {
        tag.Put("Pos", DoubleList(Pos.X, Pos.Y, Pos.Z));
        tag.Put("Motion", DoubleList(Velocity.X, Velocity.Y, Velocity.Z));
        tag.Put("Rotation", FloatList(YRot, XRot));
        tag.PutIntArray("UUID", UuidToIntArray(Uuid));
        tag.PutBoolean("OnGround", OnGround);
        if (SavesHealth) tag.PutFloat("Health", Health);
        //Tags are base behavior and are written only when non-empty, matching vanilla saveWithoutId's handling of Tags
        if (_tags.Count > 0) tag.Put(TagsTag, StringList(_tags));
        //Attributes are saved only on living entities; TakesFallDamage stands in for the vanilla LivingEntity layer here
        if (TakesFallDamage) Attributes.WriteTo(tag);
        AddAdditionalSaveData(tag);
    }

    //Load reads entity state back from a save, maps to vanilla Entity.load
    //Missing fields or type mismatches keep the current value so a single bad field does not discard the whole entity
    public virtual void Load(CompoundTag tag)
    {
        if (ReadDoubleList(tag.GetList("Pos"), 3) is { } pos) Pos = new Vec3(pos[0], pos[1], pos[2]);
        if (ReadDoubleList(tag.GetList("Motion"), 3) is { } motion) Velocity = new Vec3(motion[0], motion[1], motion[2]);
        if (ReadFloatList(tag.GetList("Rotation"), 2) is { } rotation)
        {
            YRot = rotation[0];
            XRot = rotation[1];
        }
        //UUID is an array of 4 ints; an incorrect length is treated as invalid and the original value is kept
        if (tag.GetIntArray("UUID") is { } uuid && IntArrayToUuid(uuid.Value) is { } parsed) Uuid = parsed;
        OnGround = tag.GetBooleanOr("OnGround", false);
        if (SavesHealth && tag.GetFloat("Health") is { } health) SetHealth(health.Value);
        //Tags are cleared before reading, matching vanilla load's tags.clear followed by addAll
        _tags.Clear();
        if (tag.GetList(TagsTag) is { } tagList)
            for (var i = 0; i < tagList.Count; i++)
                if (tagList.GetString(i) is { } entry) _tags.Add(entry.Value);
        if (TakesFallDamage) Attributes.ReadFrom(tag);
        ReadAdditionalSaveData(tag);
    }

    //AddAdditionalSaveData hook for subclasses to add persistence fields, maps to the vanilla method of the same name
    protected virtual void AddAdditionalSaveData(CompoundTag tag) { }

    //ReadAdditionalSaveData hook for subclasses to read extended fields back, maps to the vanilla method of the same name
    protected virtual void ReadAdditionalSaveData(CompoundTag tag) { }

    //DoubleList builds a double list, maps to vanilla ListTag of DoubleTag
    private static ListTag DoubleList(double x, double y, double z)
        => new(new Tag[] { new DoubleTag(x), new DoubleTag(y), new DoubleTag(z) });

    //FloatList builds a float list, maps to vanilla ListTag of FloatTag
    private static ListTag FloatList(float a, float b)
        => new(new Tag[] { new FloatTag(a), new FloatTag(b) });

    //StringList builds a string list, maps to vanilla ListTag of StringTag
    private static ListTag StringList(IReadOnlyCollection<string> values)
    {
        var list = new ListTag();
        foreach (var value in values) list.Add(new StringTag(value));
        return list;
    }

    //ReadDoubleList reads a fixed-length double list, returning null on a length mismatch
    private static double[]? ReadDoubleList(ListTag? list, int size)
    {
        if (list is null || list.Count != size) return null;
        var values = new double[size];
        for (var i = 0; i < size; i++)
        {
            var tag = list.GetDouble(i);
            if (tag is null) return null;
            values[i] = tag.Value;
        }
        return values;
    }

    //ReadFloatList reads a fixed-length float list, returning null on a length mismatch
    private static float[]? ReadFloatList(ListTag? list, int size)
    {
        if (list is null || list.Count != size) return null;
        var values = new float[size];
        for (var i = 0; i < size; i++)
        {
            var tag = list.GetFloat(i);
            if (tag is null) return null;
            values[i] = tag.Value;
        }
        return values;
    }

    //UuidToIntArray writes a Uuid as a 4-int big-endian sequence in the vanilla format
    //Subclasses storing Owner/Thrower style UUID fields use this too, matching vanilla UUIDUtil.CODEC
    protected static int[] UuidToIntArray(Guid uuid)
    {
        Span<byte> bytes = stackalloc byte[16];
        uuid.TryWriteBytes(bytes, bigEndian: true, out _);
        var result = new int[4];
        for (var i = 0; i < 4; i++)
            result[i] = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(i * 4, 4));
        return result;
    }

    //IntArrayToUuid restores a Uuid from the vanilla format, returning null on a length mismatch
    protected static Guid? IntArrayToUuid(int[] value)
    {
        if (value.Length != 4) return null;
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32BigEndian(bytes.Slice(i * 4, 4), value[i]);
        return new Guid(bytes, bigEndian: true);
    }

    //IsRemoved whether the entity has been marked removed, maps to vanilla Entity.isRemoved
    public bool IsRemoved { get; private set; }

    //BlocksBuilding whether the entity blocks block placement, maps to vanilla Entity.blocksBuilding
    //Off by default in vanilla, turned on by living entities in the constructor; dropped items and the like do not block placement
    public virtual bool BlocksBuilding => false;

    //Discard marks the entity for removal, maps to vanilla Entity.discard
    //It only sets a flag instead of removing immediately, to avoid mutating collections during entity tick iteration; EntityManager removes them at the end of the tick
    public void Discard() => IsRemoved = true;

    //SetPos sets Pos and angles together, aligning with vanilla moveTo/moveTo
    public void SetPos(Vec3 pos, float yRot, float xRot)
    {
        Pos = pos;
        YRot = yRot;
        XRot = xRot;
    }

    //Tick called every frame; base physics is gravity and advancing position by velocity
    //Subclasses overriding should call base.Tick first to keep gravity and movement, then layer their own behavior
    public virtual void Tick()
    {
        TickBase();
        ApplyDefaultPhysics();
    }

    //TickBase advances the life tick count and invulnerability ticks, a minimal subset of vanilla Entity.baseTick
    //Subclasses with their own physics call this instead, avoiding double movement with the base default physics
    protected void TickBase()
    {
        TickCount++;
        PreviousPos = Pos;
        //Invulnerability ticks decrement each tick, matching vanilla LivingEntity.tick's invulnerableTime--
        if (InvulnerableTime > 0) InvulnerableTime--;
    }

    //ApplyDefaultPhysics default physics: gravity reduces vertical speed, horizontal speed decays by drag, then position advances by velocity
    protected void ApplyDefaultPhysics()
    {
        Velocity = new Vec3(
            Velocity.X * HorizontalDrag,
            (Velocity.Y - DefaultGravity) * VerticalDrag,
            Velocity.Z * HorizontalDrag);
        Move(Velocity);
    }

    //ApplyKnockback applies knockback, maps to vanilla LivingEntity.knockback
    //Horizontal velocity is halved then a reversed push is added; on the ground vertical velocity becomes min(0.4, half the original + power) and in the air it is unchanged
    //Too-small direction components are randomized, matching vanilla's avoidance of purely vertical knockback
    public void ApplyKnockback(double power, double xd, double zd)
    {
        var effective = power * (1.0 - KnockbackResistance);
        if (effective <= 0.0) return;
        while (xd * xd + zd * zd < KnockbackDirectionEpsilon)
        {
            xd = (_random.NextDouble() - _random.NextDouble()) * 0.01;
            zd = (_random.NextDouble() - _random.NextDouble()) * 0.01;
        }
        var push = new Vec3(xd, 0.0, zd).Normalize().Multiply(effective);
        Velocity = new Vec3(
            Velocity.X / 2.0 - push.X,
            OnGround ? Math.Min(0.4, Velocity.Y / 2.0 + effective) : Velocity.Y,
            Velocity.Z / 2.0 - push.Z);
    }

    //CheckFallDamage accumulates fall distance and settles it on landing, maps to vanilla Entity.checkFallDamage
    //Falling in water does not accumulate; on landing the landing hook handles it and then it resets to zero
    //The parameter is the clipped vertical displacement rather than the raw one, so sliding along the ground is not counted as falling
    private void CheckFallDamage(double ya)
    {
        if (!IsInWater && ya < 0.0) FallDistance -= ya;
        if (!OnGround) return;
        if (FallDistance > 0.0) OnLandedOnGround(FallDistance);
        ResetFallDistance();
    }

    //OnLandedOnGround landing hook, the default implementation of vanilla Block.fallOn
    //There is no block behavior layer here, so damage is settled directly from the fall distance; the differences for special blocks (hay, beds, slime) await block behavior
    protected virtual void OnLandedOnGround(double fallDistance) => CauseFallDamage(fallDistance, 1.0f);

    //ResetFallDistance resets the fall distance, maps to vanilla resetFallDistance
    public void ResetFallDistance() => FallDistance = 0.0;

    //CauseFallDamage settles fall damage, maps to vanilla Entity.causeFallDamage and LivingEntity.causeFallDamage
    //The base takes no damage; living subclasses enable TakesFallDamage and lose health by distance
    public virtual bool CauseFallDamage(double fallDistance, float damageModifier)
    {
        if (!TakesFallDamage) return false;
        var damage = CalculateFallDamage(fallDistance, damageModifier);
        return damage > 0 && Hurt(damage);
    }

    //CalculateFallDamage computes damage from fall distance, maps to vanilla LivingEntity.calculateFallDamage
    //The part beyond the safe distance is multiplied by the modifier and floored; within the safe distance it yields 0 or negative, meaning no damage
    protected int CalculateFallDamage(double fallDistance, float damageModifier)
        => Mth.Floor(((fallDistance + 1.0E-6) - SafeFallDistance) * damageModifier * FallDamageMultiplier);

    //Move advances position by the delta, maps to vanilla Entity.move
    //Collision follows vanilla collideBoundingBox, taking collision shapes in the swept region of the whole movement then clipping axis by axis
    //Blocked axes get their velocity zeroed while the others keep theirs; standing is set only when blocked downward, matching vanilla restituteMovementAfterCollisions with default restitution 0
    public void Move(Vec3 delta)
    {
        if (NoPhysics)
        {
            Pos = Pos.Add(delta);
            HorizontalCollision = false;
            VerticalCollision = false;
            VerticalCollisionBelow = false;
            return;
        }
        var movement = Collide(delta);
        //Position does not advance when displacement is too small, avoiding jitter while touching a surface, matching the AND of vanilla's two thresholds
        if (movement.LengthSqr() > MovementEpsilon
            || delta.LengthSqr() - movement.LengthSqr() < MovementEpsilon)
            Pos = Pos.Add(movement);

        var xCollision = !Mth.Equal(delta.X, movement.X);
        var zCollision = !Mth.Equal(delta.Z, movement.Z);
        HorizontalCollision = xCollision || zCollision;
        var movedVertically = Math.Abs(delta.Y) > 0.0;
        //Standing is not judged when vertical displacement is 0, otherwise sliding along a wall would be misread as landing; vanilla does the same
        if (movedVertically)
        {
            VerticalCollision = delta.Y != movement.Y;
            VerticalCollisionBelow = VerticalCollision && delta.Y < 0.0;
            OnGround = VerticalCollisionBelow;
        }
        //Velocity only changes when blocked horizontally or vertically, vanilla restituteMovementAfterCollisions with default restitution 0
        //Blocked axes are zeroed and unblocked axes keep their velocity; using the clipped displacement as velocity would keep the entity pressed against the wall
        var verticalBlocked = movedVertically && VerticalCollision;
        if (HorizontalCollision || verticalBlocked)
        {
            Velocity = new Vec3(
                xCollision ? 0.0 : Velocity.X,
                verticalBlocked ? 0.0 : Velocity.Y,
                zCollision ? 0.0 : Velocity.Z);
        }
        //Fall distance accumulates from the clipped displacement and damage is settled on landing, matching checkFallDamage at the end of vanilla move
        CheckFallDamage(movement.Y);
    }

    //Collide clips the displacement so it hits no collision shape, maps to vanilla Entity.collide
    //The world border system and automatic stepping are not wired up; the world border never blocks and stepping awaits entities with a step height
    private Vec3 Collide(Vec3 movement)
    {
        if (CollisionShapes is not { } query) return movement;
        var box = BoundingBox;
        //The shape query is lazy and axis-by-axis clipping iterates repeatedly, so it is materialized into a list first
        var colliders = query(box.ExpandTowards(movement));
        return colliders.Count == 0 ? movement : CollideWithShapes(movement, box, colliders);
    }

    //CollideWithShapes clips displacement axis by axis, maps to vanilla Entity.collideWithShapes
    //After resolving one axis the box is moved before the next; the axis order is decided by the displacement, see Direction.AxisStepOrder
    private static Vec3 CollideWithShapes(Vec3 movement, AABB box, IReadOnlyList<VoxelShape> shapes)
    {
        var resolved = Vec3.Zero;
        foreach (var axis in Direction.AxisStepOrder(movement))
        {
            var amount = axis.Choose(movement.X, movement.Y, movement.Z);
            if (amount == 0.0) continue;
            var clipped = Shapes.Collide(axis, box.Move(resolved), shapes, amount);
            resolved = resolved.WithAxis(axis, clipped);
        }
        return resolved;
    }
}
