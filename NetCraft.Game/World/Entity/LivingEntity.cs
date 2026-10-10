using NetCraft.Game.World.Damage;
using NetCraft.Game.World.Effect;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util;
using NetCraft.Util.Random;
//Attribute types carry their own namespace; only the needed names are taken and aliased here
using AttributeMap = NetCraft.Registry.EntityAttribute.AttributeMap;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using AttributeDef = NetCraft.Registry.EntityAttribute.Attribute;
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//LivingEntity living base class, maps to vanilla net.minecraft.world.entity.LivingEntity
//Sits between Entity and Player/Mob and owns everything vanilla puts here rather than on Entity:
//health, attributes, damage and death, knockback, fall damage, the living-only flags, equipment and effects
//The Entity base no longer carries these; entities that are not alive (dropped items, projectiles) stay on Entity
public abstract class LivingEntity : NetCraft.Registry.Entity, IEquipmentHolder, IEffectHolder
{
    //_random the entity's own random source, used for knockback direction randomization
    private readonly RandomSource _random = RandomSource.Create();

    //_attributes the entity's attribute map, swapped in by subclasses from their type's default supplier
    private AttributeMap _attributes = new(AttributeSupplier.Empty);

    //_deathHandled whether the death hook already fired, ensuring the death flow runs only once
    private bool _deathHandled;

    //Attributes entity attribute map, maps to vanilla LivingEntity.getAttributes
    public override AttributeMap? Attributes => _attributes;

    //SetAttributes installs the attribute map for this type, maps to vanilla createAttributes
    protected void SetAttributes(AttributeMap map) => _attributes = map;

    //GetAttributeValue gets the attribute's final value, maps to vanilla getAttributeValue
    public double GetAttributeValue(AttributeDef attribute) => _attributes.GetValue(attribute);

    //Equipment the equipment slots, maps to vanilla LivingEntity.createEquipment
    //Mob and Player each held their own copy before the LivingEntity layer existed
    public EntityEquipment Equipment { get; } = new();

    //GetItemBySlot returns the item in the given slot, maps to vanilla getItemBySlot
    public ItemStack GetItemBySlot(EquipmentSlot slot) => Equipment.Get(slot);

    //SetItemSlot writes the slot, maps to vanilla setItemSlot
    public void SetItemSlot(EquipmentSlot slot, ItemStack stack) => Equipment.Set(slot, stack);

    //MainHandItem/OffhandItem the two hand slots, maps to vanilla getMainHandItem/getOffhandItem
    public ItemStack MainHandItem => GetItemBySlot(EquipmentSlot.MAINHAND);
    public ItemStack OffhandItem => GetItemBySlot(EquipmentSlot.OFFHAND);

    //HasItemInSlot whether the slot holds anything, maps to vanilla hasItemInSlot
    public bool HasItemInSlot(EquipmentSlot slot) => !Equipment.Get(slot).IsEmpty();

    //Effects the active potion effects, maps to vanilla LivingEntity's effect map
    public EntityEffects Effects { get; } = new();

    //GetActiveEffects the active effect instances, maps to vanilla getActiveEffects
    public IEnumerable<MobEffectInstance> GetActiveEffects() => Effects.Map.Values;

    //GetActiveEffectsMap the active effect map keyed by effect, maps to vanilla getActiveEffectsMap
    public IReadOnlyDictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> GetActiveEffectsMap() => Effects.Map;

    //HasEffect whether the effect is active, maps to vanilla hasEffect
    public bool HasEffect(Holder<NetCraft.Registry.MobEffect> effect) => Effects.Get(effect) is not null;

    //AddEffect adds or overwrites an effect, maps to the core of vanilla addEffect
    public bool AddEffect(MobEffectInstance effect)
    {
        Effects.Add(effect.Effect, effect);
        return true;
    }

    //RemoveEffect removes one effect, maps to vanilla removeEffect
    public bool RemoveEffect(Holder<NetCraft.Registry.MobEffect> effect) => Effects.Remove(effect);

    //RemoveAllEffects clears every effect and reports whether anything was removed, maps to vanilla removeAllEffects
    public bool RemoveAllEffects()
    {
        if (Effects.Map.Count == 0) return false;
        Effects.Clear();
        return true;
    }

    //TickEffects ages the active effects and drops the expired ones, maps to the effect part of vanilla tickEffects
    public void TickEffects()
    {
        if (Effects.Map.Count == 0) return;
        List<Holder<NetCraft.Registry.MobEffect>>? expired = null;
        foreach (var (effect, instance) in Effects.Map)
        {
            instance.Tick();
            if (instance.Expired) (expired ??= new()).Add(effect);
        }
        if (expired is null) return;
        foreach (var effect in expired) Effects.Remove(effect);
    }

    //--- Walk and swing animation, maps to the vanilla LivingEntity animation state ---

    //WalkAnimation the walk cycle state, driven by the distance moved each tick
    public WalkAnimationState WalkAnimation { get; } = new();

    //Swinging whether the hand swing animation is running
    public bool Swinging { get; private set; }

    //SwingTime ticks into the current swing, maps to vanilla swingTime
    public int SwingTime { get; private set; }

    //AttackAnim/OAttackAnim swing progress this tick and the previous tick, maps to vanilla attackAnim/oAttackAnim
    public float AttackAnim { get; private set; }
    public float OAttackAnim { get; private set; }

    //Speed the movement speed used when travelling on the ground, maps to vanilla getSpeed/setSpeed
    public float Speed { get; set; } = 0.1f;

    //FlyingSpeed the movement speed used while airborne, maps to vanilla getFlyingSpeed
    protected virtual float FlyingSpeed => 0.02f;

    //Swing starts the hand swing animation, maps to vanilla LivingEntity.swing
    public void Swing()
    {
        if (!Swinging || SwingTime >= GetCurrentSwingDuration() / 2 || SwingTime < 0)
        {
            SwingTime = -1;
            Swinging = true;
        }
    }

    //GetCurrentSwingDuration ticks one swing lasts, maps to vanilla getCurrentSwingDuration; Player shortens it with haste
    protected virtual int GetCurrentSwingDuration() => 6;

    //UpdateSwingTime advances the swing animation, maps to vanilla updateSwingTime
    public void UpdateSwingTime()
    {
        var duration = GetCurrentSwingDuration();
        if (Swinging)
        {
            SwingTime++;
            if (SwingTime >= duration)
            {
                SwingTime = 0;
                Swinging = false;
            }
        }
        else
        {
            SwingTime = 0;
        }
        AttackAnim = (float)SwingTime / duration;
    }

    //CalculateEntityAnimation feeds the distance moved this tick into the walk animation, maps to vanilla calculateEntityAnimation
    public void CalculateEntityAnimation(bool useY)
    {
        var dx = Pos.X - PreviousPos.X;
        var dy = useY ? Pos.Y - PreviousPos.Y : 0.0;
        var dz = Pos.Z - PreviousPos.Z;
        var distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (IsDeadOrDying) WalkAnimation.Stop();
        else UpdateWalkAnimation(distance);
    }

    //UpdateWalkAnimation moves the walk animation toward the target speed, maps to vanilla updateWalkAnimation
    protected void UpdateWalkAnimation(float distance)
    {
        var targetSpeed = Math.Min(distance * 4.0f, 1.0f);
        WalkAnimation.Update(targetSpeed, 0.4f, IsBaby ? 3.0f : 1.0f);
    }

    //--- Travel, maps to the vanilla LivingEntity.travel ---

    //Travel advances the entity by the movement input, maps to vanilla LivingEntity.travel
    //The fluid and elytra branches need systems that are not wired up yet, so everything falls through to the air branch
    public virtual void Travel(Vec3 input) => TravelInAir(input);

    //TravelInAir walks the entity across the ground and through the air, maps to vanilla travelInAir
    //Block friction uses the vanilla default instead of the block under the feet, which needs the level's block lookup
    protected virtual void TravelInAir(Vec3 input)
    {
        var blockFriction = OnGround ? 0.6f : 1.0f;
        MoveRelative(GetFrictionInfluencedSpeed(blockFriction), input);
        Move(Velocity);
        var friction = blockFriction * HorizontalDrag;
        Velocity = new Vec3(
            Velocity.X * friction,
            (Velocity.Y - DefaultGravity) * VerticalDrag,
            Velocity.Z * friction);
    }

    //GetFrictionInfluencedSpeed the speed the movement input is applied at, maps to vanilla getFrictionInfluencedSpeed
    protected float GetFrictionInfluencedSpeed(float friction)
        => OnGround ? Speed * (0.21600002f / (friction * friction * friction)) : FlyingSpeed;

    //SafeFallDistance the distance at which fall damage starts counting, maps to vanilla attribute SAFE_FALL_DISTANCE, default 3
    public virtual double SafeFallDistance => 3.0;

    //FallDamageMultiplier fall damage multiplier, maps to vanilla attribute FALL_DAMAGE_MULTIPLIER, default 1
    public virtual double FallDamageMultiplier => 1.0;

    //KnockbackResistance knockback resistance between 0 and 1, where 1 means full immunity, maps to vanilla attribute KNOCKBACK_RESISTANCE
    public virtual double KnockbackResistance => 0.0;

    //MaxUpStep the maximum step height that can be climbed automatically, maps to vanilla maxUpStep
    public virtual double MaxUpStep => 0.0;

    //TakesFallDamage whether fall damage applies, maps to the vanilla LivingEntity fall damage path
    public virtual bool TakesFallDamage => true;

    //IsBaby whether it is a baby, maps to vanilla LivingEntity.isBaby
    public bool IsBaby { get; set; }

    //IsFallFlying whether it is elytra gliding, maps to vanilla LivingEntity.isFallFlying
    public bool IsFallFlying { get; set; }

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
    public override bool Hurt(float amount, Vec3? knockbackSource = null)
    {
        if (IsDeadOrDying || InvulnerableTime > 0) return false;
        Health = Math.Max(0f, Health - amount);
        //Invulnerability for 10 ticks; vanilla is 20 ticks with 10 for the hurt animation
        InvulnerableTime = 10;
        //Hurt knockback with strength 0.4, maps to vanilla dealDefaultKnockback
        //The direction is "source minus self" as in vanilla and knockback negates it internally; the net effect pushes the target away from the source
        if (knockbackSource is { } source)
            ApplyKnockback(0.4, source.X - Pos.X, source.Z - Pos.Z);
        if (Health <= 0f) TriggerDeath();
        return true;
    }

    //_lastHurt damage of the last hit, used by the invulnerability branch of the vanilla hurtServer flow
    private float _lastHurt;

    //AbsorptionAmount absorption health that soaks damage before the real health, maps to vanilla absorptionAmount
    public float AbsorptionAmount { get; private set; }

    //LastDamageSource the source of the most recent damaging hit, maps to vanilla lastDamageSource
    public DamageSource? LastDamageSource { get; private set; }

    //MaxAbsorption the absorption ceiling from the max_absorption attribute, maps to vanilla getMaxAbsorption
    public float MaxAbsorption => (float)GetAttributeValue(EntityAttributes.MaxAbsorption);

    //SetAbsorptionAmount sets the absorption health, maps to vanilla setAbsorptionAmount
    public void SetAbsorptionAmount(float amount) => AbsorptionAmount = Math.Max(0f, amount);

    //Hurt applies damage from a source, maps to the core of vanilla hurtServer
    //Tag checks, item blocking, enchantment protection, stats and criteria triggers need systems that are not wired up yet
    public virtual bool Hurt(float amount, DamageSource source)
    {
        if (IsDeadOrDying) return false;
        if (amount < 0f) amount = 0f;
        if (InvulnerableTime > 10)
        {
            //A stronger hit inside the invulnerability window only deals the difference, maps to the lastHurt branch
            if (amount <= _lastHurt) return false;
            ActuallyHurt(source, amount - _lastHurt);
            _lastHurt = amount;
        }
        else
        {
            _lastHurt = amount;
            InvulnerableTime = 20;
            ActuallyHurt(source, amount);
        }
        DealDefaultKnockback(source, amount, false);
        if (Health <= 0f) TriggerDeath();
        LastDamageSource = source;
        return true;
    }

    //ActuallyHurt subtracts the armor and effect reductions then the absorption, maps to vanilla actuallyHurt
    protected virtual void ActuallyHurt(DamageSource source, float damage)
    {
        damage = GetDamageAfterArmorAbsorb(source, damage);
        damage = GetDamageAfterMagicAbsorb(source, damage);
        if (AbsorptionAmount > 0f)
        {
            var absorbed = Math.Min(damage, AbsorptionAmount);
            SetAbsorptionAmount(AbsorptionAmount - absorbed);
            damage -= absorbed;
        }
        if (damage > 0f) Health = Math.Max(0f, Health - damage);
    }

    //GetDamageAfterArmorAbsorb applies the armor and armor toughness reduction, maps to vanilla getDamageAfterArmorAbsorb
    //The armor durability hit and the weapon enchantment effectiveness modifier are not wired up
    protected virtual float GetDamageAfterArmorAbsorb(DamageSource source, float damage)
    {
        var armor = (float)GetAttributeValue(EntityAttributes.Armor);
        var toughness = 2.0f + (float)GetAttributeValue(EntityAttributes.ArmorToughness) / 4.0f;
        var realArmor = Mth.Clamp(armor - damage / toughness, armor * 0.2f, 20.0f);
        return damage * (1.0f - realArmor / 25.0f);
    }

    //GetDamageAfterMagicAbsorb applies the resistance effect reduction, maps to vanilla getDamageAfterMagicAbsorb
    //Enchantment protection is not wired up, so only the resistance effect is applied
    protected virtual float GetDamageAfterMagicAbsorb(DamageSource source, float damage)
    {
        if (damage <= 0f) return 0f;
        if (Effects.Get(MobEffects.RESISTANCE) is { } resistance)
        {
            var absorb = 25 - (resistance.Amplifier + 1) * 5;
            damage = Math.Max(damage * absorb / 25.0f, 0f);
        }
        return damage;
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

    //Die hook when health reaches zero, maps to vanilla die; subclasses do drops and death effects
    protected virtual void Die() { }

    //TriggerDeath fires the death hook only once, avoiding Hurt and SetHealth running the death flow twice
    private void TriggerDeath()
    {
        if (_deathHandled) return;
        _deathHandled = true;
        Die();
        RaiseDied();
    }

    //Tick runs the base tick then decays the invulnerability frames, mirrors invulnerableTime-- in vanilla LivingEntity.tick
    public override void Tick()
    {
        OAttackAnim = AttackAnim;
        base.Tick();
        if (InvulnerableTime > 0) InvulnerableTime--;
        TickEffects();
        UpdateSwingTime();
        CalculateEntityAnimation(false);
    }

    //CauseFallDamage settles fall damage from the attributes, maps to vanilla LivingEntity.causeFallDamage
    public override bool CauseFallDamage(double fallDistance, float damageModifier)
    {
        if (!TakesFallDamage) return false;
        var damage = CalculateFallDamage(fallDistance, damageModifier);
        return damage > 0 && Hurt(damage);
    }

    //CalculateFallDamage computes damage from fall distance, maps to vanilla LivingEntity.calculateFallDamage
    //The part beyond the safe distance is multiplied by the modifier and floored; within the safe distance it yields 0 or negative, meaning no damage
    protected int CalculateFallDamage(double fallDistance, float damageModifier)
        => Mth.Floor(((fallDistance + 1.0E-6) - SafeFallDistance) * damageModifier * FallDamageMultiplier);

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

    //KnockbackDirectionEpsilon minimum squared length of the knockback direction, maps to vanilla 9.999999747378752E-6
    //When the direction component is shorter than this it is randomized, so a purely vertical knockback does not pin the entity in place
    private const double KnockbackDirectionEpsilon = 9.999999747378752E-6;

    //Knockback applies knockback from a damage source, maps to vanilla LivingEntity.knockback(power, xd, zd, source, damage, comesFromEffect)
    //The resistance attribute is applied inside, so callers pass the raw power
    //The source, damage and comesFromEffect arguments only matter to subclasses that tune the push by the kind of hit
    public void Knockback(double power, double xd, double zd, DamageSource source, float damage, bool comesFromEffect)
        => ApplyKnockback(power, xd, zd);

    //Knockback overload without the effect flag, maps to vanilla knockback(power, xd, zd, source, damage)
    public void Knockback(double power, double xd, double zd, DamageSource source, float damage)
        => Knockback(power, xd, zd, source, damage, false);

    //DealDefaultKnockback pushes the target away from where the damage came from, maps to vanilla dealDefaultKnockback
    //Vanilla asks a projectile for the horizontal knockback direction; that hook is not wired up here, so the source position is used for every source
    public void DealDefaultKnockback(DamageSource source, float damage, bool blocked)
    {
        double xd = 0.0;
        double zd = 0.0;
        if (source.DamageSourcePosition is { } position)
        {
            xd = position.X - Pos.X;
            zd = position.Z - Pos.Z;
        }
        Knockback(0.4, xd, zd, source, damage);
        if (!blocked) IndicateDamage(xd, zd);
    }

    //IndicateDamage feeds the hurt direction back to the entity; the base does nothing, maps to vanilla indicateDamage
    public virtual void IndicateDamage(double xd, double zd) { }

    //AddAdditionalSaveData writes health and attributes, maps to the living part of vanilla LivingEntity.addAdditionalSaveData
    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        tag.PutFloat("Health", Health);
        _attributes.WriteTo(tag);
        base.AddAdditionalSaveData(tag);
    }

    //ReadAdditionalSaveData reads health and attributes back; a missing field keeps the current value
    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        if (tag.GetFloat("Health") is { } health) SetHealth(health.Value);
        _attributes.ReadFrom(tag);
        base.ReadAdditionalSaveData(tag);
    }
}
