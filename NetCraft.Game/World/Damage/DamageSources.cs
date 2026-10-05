using NetCraft.Registry;

namespace NetCraft.Game.World.Damage;

//DamageSources 伤害来源工厂 对应原版 net.minecraft.world.damagesource.DamageSources
//按注册表预建无参来源 并给出各种带实体来源的工厂
//原版针对投射物各类型(箭/火球/凋灵头)的专门工厂依赖那些投射物类型 未接通故统一收实体参数
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

    //Source 按资源键建一个无实体来源
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

    //FallingBlock 坠落方块砸中 对应原版 fallingBlock
    public DamageSource FallingBlock(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.FALLING_BLOCK, entity);

    //Anvil 铁砧砸中 对应原版 anvil
    public DamageSource Anvil(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.FALLING_ANVIL, entity);

    //FallingStalactite 钟乳石砸中 对应原版 fallingStalactite
    public DamageSource FallingStalactite(NetCraft.Registry.Entity entity)
        => WithEntity(DamageTypes.FALLING_STALACTITE, entity);

    //Sting 蜂刺 对应原版 sting
    public DamageSource Sting(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.STING, entity);

    //MobAttack 生物近战 对应原版 mobAttack
    public DamageSource MobAttack(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.MOB_ATTACK, entity);

    //NoAggroMobAttack 不引仇恨的生物近战 对应原版 noAggroMobAttack
    public DamageSource NoAggroMobAttack(NetCraft.Registry.Entity entity)
        => WithEntity(DamageTypes.MOB_ATTACK_NO_AGGRO, entity);

    //PlayerAttack 玩家近战 对应原版 playerAttack
    public DamageSource PlayerAttack(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.PLAYER_ATTACK, entity);

    //Thorns 荆棘反伤 对应原版 thorns
    public DamageSource Thorns(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.THORNS, entity);

    //Mace 重锤猛击 对应原版 mace
    public DamageSource Mace(NetCraft.Registry.Entity entity) => WithEntity(DamageTypes.MACE_SMASH, entity);

    //Arrow 箭矢 直接来源是箭真凶是射手 对应原版 arrow
    public DamageSource Arrow(NetCraft.Registry.Entity arrow, NetCraft.Registry.Entity? shooter)
        => Indirect(DamageTypes.ARROW, arrow, shooter);

    //Trident 三叉戟 对应原版 trident
    public DamageSource Trident(NetCraft.Registry.Entity trident, NetCraft.Registry.Entity? thrower)
        => Indirect(DamageTypes.TRIDENT, trident, thrower);

    //MobProjectile 生物投射物 对应原版 mobProjectile
    public DamageSource MobProjectile(NetCraft.Registry.Entity projectile, NetCraft.Registry.Entity? owner)
        => Indirect(DamageTypes.MOB_PROJECTILE, projectile, owner);

    //Thrown 投掷物 对应原版 thrown
    public DamageSource Thrown(NetCraft.Registry.Entity projectile, NetCraft.Registry.Entity? thrower)
        => Indirect(DamageTypes.THROWN, projectile, thrower);

    //IndirectMagic 间接魔法伤害 对应原版 indirectMagic
    public DamageSource IndirectMagic(NetCraft.Registry.Entity direct, NetCraft.Registry.Entity? causing)
        => Indirect(DamageTypes.INDIRECT_MAGIC, direct, causing);

    //Explosion 爆炸 直接来源与真凶同源 对应原版 explosion(Entity, Entity)
    public DamageSource Explosion(NetCraft.Registry.Entity direct, NetCraft.Registry.Entity? causing)
        => Indirect(DamageTypes.EXPLOSION, direct, causing);

    //WithEntity 真凶与直接来源为同一实体
    private DamageSource WithEntity(ResourceKey<DamageType> key, NetCraft.Registry.Entity entity)
        => new(_damageTypes.WrapAsHolder(_damageTypes.GetValueOrThrow(key)), entity, entity);

    //Indirect 直接来源与真凶分开
    private DamageSource Indirect(ResourceKey<DamageType> key, NetCraft.Registry.Entity direct,
        NetCraft.Registry.Entity? causing)
        => new(_damageTypes.WrapAsHolder(_damageTypes.GetValueOrThrow(key)), direct, causing);
}
