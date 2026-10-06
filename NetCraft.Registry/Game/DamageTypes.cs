namespace NetCraft.Registry;

//DamageTypes built-in damage types, maps to vanilla net.minecraft.world.damagesource.DamageTypes
//The constants are resource keys; entries are registered by Bootstrap in vanilla declaration order
public static class DamageTypes
{
    public static readonly ResourceKey<DamageType> IN_FIRE = Key("in_fire");
    public static readonly ResourceKey<DamageType> CAMPFIRE = Key("campfire");
    public static readonly ResourceKey<DamageType> LIGHTNING_BOLT = Key("lightning_bolt");
    public static readonly ResourceKey<DamageType> ON_FIRE = Key("on_fire");
    public static readonly ResourceKey<DamageType> LAVA = Key("lava");
    public static readonly ResourceKey<DamageType> HOT_FLOOR = Key("hot_floor");
    public static readonly ResourceKey<DamageType> SULFUR_CUBE_HOT = Key("sulfur_cube_hot");
    public static readonly ResourceKey<DamageType> IN_WALL = Key("in_wall");
    public static readonly ResourceKey<DamageType> CRAMMING = Key("cramming");
    public static readonly ResourceKey<DamageType> DROWN = Key("drown");
    public static readonly ResourceKey<DamageType> STARVE = Key("starve");
    public static readonly ResourceKey<DamageType> CACTUS = Key("cactus");
    public static readonly ResourceKey<DamageType> FALL = Key("fall");
    public static readonly ResourceKey<DamageType> ENDER_PEARL = Key("ender_pearl");
    public static readonly ResourceKey<DamageType> FLY_INTO_WALL = Key("fly_into_wall");
    public static readonly ResourceKey<DamageType> FELL_OUT_OF_WORLD = Key("out_of_world");
    public static readonly ResourceKey<DamageType> GENERIC = Key("generic");
    public static readonly ResourceKey<DamageType> MAGIC = Key("magic");
    public static readonly ResourceKey<DamageType> WITHER = Key("wither");
    public static readonly ResourceKey<DamageType> DRAGON_BREATH = Key("dragon_breath");
    public static readonly ResourceKey<DamageType> DRY_OUT = Key("dry_out");
    public static readonly ResourceKey<DamageType> SWEET_BERRY_BUSH = Key("sweet_berry_bush");
    public static readonly ResourceKey<DamageType> FREEZE = Key("freeze");
    public static readonly ResourceKey<DamageType> STALAGMITE = Key("stalagmite");
    public static readonly ResourceKey<DamageType> FALLING_BLOCK = Key("falling_block");
    public static readonly ResourceKey<DamageType> FALLING_ANVIL = Key("falling_anvil");
    public static readonly ResourceKey<DamageType> FALLING_STALACTITE = Key("falling_stalactite");
    public static readonly ResourceKey<DamageType> STING = Key("sting");
    public static readonly ResourceKey<DamageType> MOB_ATTACK = Key("mob_attack");
    public static readonly ResourceKey<DamageType> MOB_ATTACK_NO_AGGRO = Key("mob_attack_no_aggro");
    public static readonly ResourceKey<DamageType> PLAYER_ATTACK = Key("player_attack");
    public static readonly ResourceKey<DamageType> SPEAR = Key("spear");
    public static readonly ResourceKey<DamageType> ARROW = Key("arrow");
    public static readonly ResourceKey<DamageType> TRIDENT = Key("trident");
    public static readonly ResourceKey<DamageType> MOB_PROJECTILE = Key("mob_projectile");
    public static readonly ResourceKey<DamageType> SPIT = Key("spit");
    public static readonly ResourceKey<DamageType> WIND_CHARGE = Key("wind_charge");
    public static readonly ResourceKey<DamageType> FIREWORKS = Key("fireworks");
    public static readonly ResourceKey<DamageType> FIREBALL = Key("fireball");
    public static readonly ResourceKey<DamageType> UNATTRIBUTED_FIREBALL = Key("unattributed_fireball");
    public static readonly ResourceKey<DamageType> WITHER_SKULL = Key("wither_skull");
    public static readonly ResourceKey<DamageType> THROWN = Key("thrown");
    public static readonly ResourceKey<DamageType> INDIRECT_MAGIC = Key("indirect_magic");
    public static readonly ResourceKey<DamageType> THORNS = Key("thorns");
    public static readonly ResourceKey<DamageType> EXPLOSION = Key("explosion");
    public static readonly ResourceKey<DamageType> PLAYER_EXPLOSION = Key("player_explosion");
    public static readonly ResourceKey<DamageType> SONIC_BOOM = Key("sonic_boom");
    public static readonly ResourceKey<DamageType> BAD_RESPAWN_POINT = Key("bad_respawn_point");
    public static readonly ResourceKey<DamageType> OUTSIDE_BORDER = Key("outside_border");
    public static readonly ResourceKey<DamageType> GENERIC_KILL = Key("generic_kill");
    public static readonly ResourceKey<DamageType> MACE_SMASH = Key("mace_smash");

    //Key builds a damage type resource key under the default namespace
    private static ResourceKey<DamageType> Key(string path)
        => ResourceKey<DamageType>.Create(Registries.DAMAGE_TYPE, Identifier.WithDefaultNamespace(path));

    //Bootstrap registers all built-in damage types into DAMAGE_TYPE, ordered to match vanilla bootstrap
    public static void Bootstrap()
    {
        Register(IN_FIRE, new DamageType("inFire", 0.1f, DamageEffects.BURNING));
        Register(CAMPFIRE, new DamageType("inFire", 0.1f, DamageEffects.BURNING));
        Register(LIGHTNING_BOLT, new DamageType("lightningBolt", 0.1f));
        Register(ON_FIRE, new DamageType("onFire", 0.0f, DamageEffects.BURNING));
        Register(LAVA, new DamageType("lava", 0.1f, DamageEffects.BURNING));
        Register(HOT_FLOOR, new DamageType("hotFloor", 0.1f, DamageEffects.BURNING));
        Register(SULFUR_CUBE_HOT, new DamageType("sulfurCubeHot", 0.1f, DamageEffects.BURNING));
        Register(IN_WALL, new DamageType("inWall", 0.0f));
        Register(CRAMMING, new DamageType("cramming", 0.0f));
        Register(DROWN, new DamageType("drown", 0.0f, DamageEffects.DROWNING));
        Register(STARVE, new DamageType("starve", 0.0f));
        Register(CACTUS, new DamageType("cactus", 0.1f));
        Register(FALL, new DamageType("fall", DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER, 0.0f,
            DamageEffects.HURT, DeathMessageType.FALL_VARIANTS));
        Register(ENDER_PEARL, new DamageType("fall", DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER, 0.0f,
            DamageEffects.HURT, DeathMessageType.FALL_VARIANTS));
        Register(FLY_INTO_WALL, new DamageType("flyIntoWall", 0.0f));
        Register(FELL_OUT_OF_WORLD, new DamageType("outOfWorld", 0.0f));
        Register(GENERIC, new DamageType("generic", 0.0f));
        Register(MAGIC, new DamageType("magic", 0.0f));
        Register(WITHER, new DamageType("wither", 0.0f));
        Register(DRAGON_BREATH, new DamageType("dragonBreath", 0.0f));
        Register(DRY_OUT, new DamageType("dryout", 0.1f));
        Register(SWEET_BERRY_BUSH, new DamageType("sweetBerryBush", 0.1f, DamageEffects.POKING));
        Register(FREEZE, new DamageType("freeze", 0.0f, DamageEffects.FREEZING));
        Register(STALAGMITE, new DamageType("stalagmite", 0.0f));
        Register(FALLING_BLOCK, new DamageType("fallingBlock", 0.1f));
        Register(FALLING_ANVIL, new DamageType("anvil", 0.1f));
        Register(FALLING_STALACTITE, new DamageType("fallingStalactite", 0.1f));
        Register(STING, new DamageType("sting", 0.1f));
        Register(MOB_ATTACK, new DamageType("mob", 0.1f));
        Register(MOB_ATTACK_NO_AGGRO, new DamageType("mob", 0.1f));
        Register(PLAYER_ATTACK, new DamageType("player", 0.1f));
        Register(SPEAR, new DamageType("spear", 0.1f));
        Register(ARROW, new DamageType("arrow", 0.1f));
        Register(TRIDENT, new DamageType("trident", 0.1f));
        Register(MOB_PROJECTILE, new DamageType("mob", 0.1f));
        Register(SPIT, new DamageType("mob", 0.1f));
        Register(FIREWORKS, new DamageType("fireworks", 0.1f));
        Register(UNATTRIBUTED_FIREBALL, new DamageType("onFire", 0.1f, DamageEffects.BURNING));
        Register(FIREBALL, new DamageType("fireball", 0.1f, DamageEffects.BURNING));
        Register(WITHER_SKULL, new DamageType("witherSkull", 0.1f));
        Register(THROWN, new DamageType("thrown", 0.1f));
        Register(INDIRECT_MAGIC, new DamageType("indirectMagic", 0.0f));
        Register(THORNS, new DamageType("thorns", 0.1f, DamageEffects.THORNS));
        Register(EXPLOSION, new DamageType("explosion", DamageScaling.ALWAYS, 0.1f));
        Register(PLAYER_EXPLOSION, new DamageType("explosion.player", DamageScaling.ALWAYS, 0.1f));
        Register(SONIC_BOOM, new DamageType("sonic_boom", DamageScaling.ALWAYS, 0.0f));
        Register(BAD_RESPAWN_POINT, new DamageType("badRespawnPoint", DamageScaling.ALWAYS, 0.1f,
            DamageEffects.HURT, DeathMessageType.INTENTIONAL_GAME_DESIGN));
        Register(OUTSIDE_BORDER, new DamageType("outsideBorder", 0.0f));
        Register(GENERIC_KILL, new DamageType("genericKill", 0.0f));
        Register(WIND_CHARGE, new DamageType("mob", 0.1f));
        Register(MACE_SMASH, new DamageType("mace_smash", 0.1f));
    }

    //Register registers a damage type by resource key
    private static void Register(ResourceKey<DamageType> key, DamageType type)
        => Registry<DamageType>.Register(BuiltInRegistries.DAMAGE_TYPE, key, type);
}
