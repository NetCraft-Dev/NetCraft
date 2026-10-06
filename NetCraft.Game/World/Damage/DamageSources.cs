using NetCraft.Registry;

namespace NetCraft.Game.World.Damage;

//DamageSources damage source factory, maps to vanilla net.minecraft.world.damagesource.DamageSources
//Prebuilds parameterless sources from the registry and provides various factories with entity sources
//Vanilla has dedicated factories per projectile type (arrow/fireball/wither skull) that depend on those projectile types; not wired up here, so they take an entity argument uniformly
public sealed class DamageSources
{
    private readonly Registry<DamageType> _damageTypes;

    private readonly DamageSource _inFire;
    private readonly DamageSource _campfire;
    private readonly DamageSource _lightningBolt;
    private readonly DamageSource _onFire;
    private readonly DamageSource _lava;
    private readonly DamageSource _hotFloor;
    private readonly DamageSource _inWall;
    private readonly DamageSource _cramming;
    private readonly DamageSource _drown;
    private readonly DamageSource _starve;
    private readonly DamageSource _cactus;
    private readonly DamageSource _fall;
    private readonly DamageSource _enderPearl;
    private readonly DamageSource _flyIntoWall;
    private readonly DamageSource _fellOutOfWorld;
    private readonly DamageSource _generic;
    private readonly DamageSource _magic;
    private readonly DamageSource _wither;
    private readonly DamageSource _dragonBreath;
    private readonly DamageSource _dryOut;
    private readonly DamageSource _sweetBerryBush;
    private readonly DamageSource _freeze;
    private readonly DamageSource _stalagmite;
    private readonly DamageSource _outOfBorder;
    private readonly DamageSource _genericKill;

    public DamageSources(Registry<DamageType> damageTypes)
    {
        _damageTypes = damageTypes;
        _inFire = Source(DamageTypes.IN_FIRE);
        _campfire = Source(DamageTypes.CAMPFIRE);
        _lightningBolt = Source(DamageTypes.LIGHTNING_BOLT);
        _onFire = Source(DamageTypes.ON_FIRE);
        _lava = Source(DamageTypes.LAVA);
        _hotFloor = Source(DamageTypes.HOT_FLOOR);
        _inWall = Source(DamageTypes.IN_WALL);
        _cramming = Source(DamageTypes.CRAMMING);
        _drown = Source(DamageTypes.DROWN);
        _starve = Source(DamageTypes.STARVE);
        _cactus = Source(DamageTypes.CACTUS);
        _fall = Source(DamageTypes.FALL);
        _enderPearl = Source(DamageTypes.ENDER_PEARL);
        _flyIntoWall = Source(DamageTypes.FLY_INTO_WALL);
        _fellOutOfWorld = Source(DamageTypes.FELL_OUT_OF_WORLD);
        _generic = Source(DamageTypes.GENERIC);
        _magic = Source(DamageTypes.MAGIC);
        _wither = Source(DamageTypes.WITHER);
        _dragonBreath = Source(DamageTypes.DRAGON_BREATH);
        _dryOut = Source(DamageTypes.DRY_OUT);
        _sweetBerryBush = Source(DamageTypes.SWEET_BERRY_BUSH);
        _freeze = Source(DamageTypes.FREEZE);
        _stalagmite = Source(DamageTypes.STALAGMITE);
        _outOfBorder = Source(DamageTypes.OUTSIDE_BORDER);
        _genericKill = Source(DamageTypes.GENERIC_KILL);
    }

    //Source builds a source without an entity from a resource key
    private DamageSource Source(ResourceKey<DamageType> key)
        => new(_damageTypes.WrapAsHolder(_damageTypes.GetValueOrThrow(key)), null, null);

    public DamageSource InFire() => _inFire;

    public DamageSource Campfire() => _campfire;

    public DamageSource LightningBolt() => _lightningBolt;

    public DamageSource OnFire() => _onFire;

    public DamageSource Lava() => _lava;

    public DamageSource HotFloor() => _hotFloor;

    public DamageSource InWall() => _inWall;

    public DamageSource Cramming() => _cramming;

    public DamageSource Drown() => _drown;

    public DamageSource Starve() => _starve;

    public DamageSource Cactus() => _cactus;

    public DamageSource Fall() => _fall;

    public DamageSource EnderPearl() => _enderPearl;

    public DamageSource FlyIntoWall() => _flyIntoWall;

    public DamageSource FellOutOfWorld() => _fellOutOfWorld;

    public DamageSource Generic() => _generic;

    public DamageSource Magic() => _magic;

    public DamageSource Wither() => _wither;

    public DamageSource DragonBreath() => _dragonBreath;

    public DamageSource DryOut() => _dryOut;

    public DamageSource SweetBerryBush() => _sweetBerryBush;

    public DamageSource Freeze() => _freeze;

    public DamageSource Stalagmite() => _stalagmite;

    public DamageSource OutOfBorder() => _outOfBorder;

    public DamageSource GenericKill() => _genericKill;

    //FallingBlock hit by a falling block, maps to vanilla fallingBlock
    public DamageSource FallingBlock(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.FALLING_BLOCK, entity);

    //Anvil hit by an anvil, maps to vanilla anvil
    public DamageSource Anvil(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.FALLING_ANVIL, entity);

    //FallingStalactite hit by a falling stalactite, maps to vanilla fallingStalactite
    public DamageSource FallingStalactite(NetCraft.Registry.Entity entity)
        => WithEntity(DamageTypes.FALLING_STALACTITE, entity);

    //Sting bee sting, maps to vanilla sting
    public DamageSource Sting(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.STING, entity);

    //MobAttack mob melee, maps to vanilla mobAttack
    public DamageSource MobAttack(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.MOB_ATTACK, entity);

    //NoAggroMobAttack mob melee that does not draw aggro, maps to vanilla noAggroMobAttack
    public DamageSource NoAggroMobAttack(NetCraft.Registry.Entity entity)
        => WithEntity(DamageTypes.MOB_ATTACK_NO_AGGRO, entity);

    //PlayerAttack player melee, maps to vanilla playerAttack
    public DamageSource PlayerAttack(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.PLAYER_ATTACK, entity);

    //Thorns thorns recoil, maps to vanilla thorns
    public DamageSource Thorns(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.THORNS, entity);

    //Mace mace smash, maps to vanilla mace
    public DamageSource Mace(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.MACE_SMASH, entity);

    //Arrow arrow; the direct source is the arrow, the culprit is the shooter, maps to vanilla arrow
    public DamageSource Arrow(NetCraft.Registry.Entity arrow, NetCraft.Registry.Entity? shooter)
        => Indirect(DamageTypes.ARROW, arrow, shooter);

    //Trident trident, maps to vanilla trident
    public DamageSource Trident(NetCraft.Registry.Entity trident, NetCraft.Registry.Entity? thrower)
        => Indirect(DamageTypes.TRIDENT, trident, thrower);

    //MobProjectile mob projectile, maps to vanilla mobProjectile
    public DamageSource MobProjectile(NetCraft.Registry.Entity projectile, NetCraft.Registry.Entity? owner)
        => Indirect(DamageTypes.MOB_PROJECTILE, projectile, owner);

    //Thrown thrown item, maps to vanilla thrown
    public DamageSource Thrown(NetCraft.Registry.Entity projectile, NetCraft.Registry.Entity? thrower)
        => Indirect(DamageTypes.THROWN, projectile, thrower);

    //IndirectMagic indirect magic damage, maps to vanilla indirectMagic
    public DamageSource IndirectMagic(NetCraft.Registry.Entity direct, NetCraft.Registry.Entity? causing)
        => Indirect(DamageTypes.INDIRECT_MAGIC, direct, causing);

    //Explosion explosion; the direct source and the culprit are the same, maps to vanilla explosion(Entity, Entity)
    public DamageSource Explosion(NetCraft.Registry.Entity direct, NetCraft.Registry.Entity? causing)
        => Indirect(DamageTypes.EXPLOSION, direct, causing);

    //WithEntity the culprit and direct source are the same entity
    private DamageSource WithEntity(ResourceKey<DamageType> key, NetCraft.Registry.Entity entity)
        => new(_damageTypes.WrapAsHolder(_damageTypes.GetValueOrThrow(key)), entity, entity);

    //Indirect the direct source and the culprit are separate
    private DamageSource Indirect(ResourceKey<DamageType> key, NetCraft.Registry.Entity direct,
        NetCraft.Registry.Entity? causing)
        => new(_damageTypes.WrapAsHolder(_damageTypes.GetValueOrThrow(key)), direct, causing);
}
