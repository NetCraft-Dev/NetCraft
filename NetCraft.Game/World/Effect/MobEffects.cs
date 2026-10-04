namespace NetCraft.Game.World.Effect;

//MobEffects 内置药水效果对应原版 net.minecraft.world.effect.MobEffects
//注册顺序严格按原版静态字段声明顺序 注册表 id 即客户端注册表 id 错位客户端会显示错效果
//字段初始化器按声明顺序执行 首次访问任一成员即完成全部登记 早于注册表冻结
public static class MobEffects
{
    private static bool _bootstrapped;

    public static readonly Holder<NetCraft.Registry.MobEffect> SPEED = Register("speed", new MobEffect(MobEffectCategory.Beneficial, 3402751));
    public static readonly Holder<NetCraft.Registry.MobEffect> SLOWNESS = Register("slowness", new MobEffect(MobEffectCategory.Harmful, 9154528));
    public static readonly Holder<NetCraft.Registry.MobEffect> HASTE = Register("haste", new MobEffect(MobEffectCategory.Beneficial, 14270531));
    public static readonly Holder<NetCraft.Registry.MobEffect> MINING_FATIGUE = Register("mining_fatigue", new MobEffect(MobEffectCategory.Harmful, 4866583));
    public static readonly Holder<NetCraft.Registry.MobEffect> STRENGTH = Register("strength", new MobEffect(MobEffectCategory.Beneficial, 16762624));
    public static readonly Holder<NetCraft.Registry.MobEffect> INSTANT_HEALTH = Register("instant_health", new HealOrHarmMobEffect(MobEffectCategory.Beneficial, 16262179));
    public static readonly Holder<NetCraft.Registry.MobEffect> INSTANT_DAMAGE = Register("instant_damage", new HealOrHarmMobEffect(MobEffectCategory.Harmful, 11101546));
    public static readonly Holder<NetCraft.Registry.MobEffect> JUMP_BOOST = Register("jump_boost", new MobEffect(MobEffectCategory.Beneficial, 16646020));
    //原版 setBlendDuration(150, 20, 60) 本作只需触发 NeedsBlend 三个时长参数用首参代表
    public static readonly Holder<NetCraft.Registry.MobEffect> NAUSEA = Register("nausea", new MobEffect(MobEffectCategory.Harmful, 5578058).SetBlendDuration(150));
    public static readonly Holder<NetCraft.Registry.MobEffect> REGENERATION = Register("regeneration", new MobEffect(MobEffectCategory.Beneficial, 13458603));
    public static readonly Holder<NetCraft.Registry.MobEffect> RESISTANCE = Register("resistance", new MobEffect(MobEffectCategory.Beneficial, 9520880));
    public static readonly Holder<NetCraft.Registry.MobEffect> FIRE_RESISTANCE = Register("fire_resistance", new MobEffect(MobEffectCategory.Beneficial, 0xFF9900));
    public static readonly Holder<NetCraft.Registry.MobEffect> WATER_BREATHING = Register("water_breathing", new MobEffect(MobEffectCategory.Beneficial, 10017472));
    public static readonly Holder<NetCraft.Registry.MobEffect> INVISIBILITY = Register("invisibility", new MobEffect(MobEffectCategory.Beneficial, 0xF6F6F6));
    public static readonly Holder<NetCraft.Registry.MobEffect> BLINDNESS = Register("blindness", new MobEffect(MobEffectCategory.Harmful, 2039587));
    public static readonly Holder<NetCraft.Registry.MobEffect> NIGHT_VISION = Register("night_vision", new MobEffect(MobEffectCategory.Beneficial, 12779366));
    public static readonly Holder<NetCraft.Registry.MobEffect> HUNGER = Register("hunger", new MobEffect(MobEffectCategory.Harmful, 5797459));
    public static readonly Holder<NetCraft.Registry.MobEffect> WEAKNESS = Register("weakness", new MobEffect(MobEffectCategory.Harmful, 0x484D48));
    public static readonly Holder<NetCraft.Registry.MobEffect> POISON = Register("poison", new MobEffect(MobEffectCategory.Harmful, 8889187));
    public static readonly Holder<NetCraft.Registry.MobEffect> WITHER = Register("wither", new MobEffect(MobEffectCategory.Harmful, 7561558));
    public static readonly Holder<NetCraft.Registry.MobEffect> HEALTH_BOOST = Register("health_boost", new MobEffect(MobEffectCategory.Beneficial, 16284963));
    public static readonly Holder<NetCraft.Registry.MobEffect> ABSORPTION = Register("absorption", new MobEffect(MobEffectCategory.Beneficial, 0x2552A5));
    public static readonly Holder<NetCraft.Registry.MobEffect> SATURATION = Register("saturation", new HealOrHarmMobEffect(MobEffectCategory.Beneficial, 16262179));
    public static readonly Holder<NetCraft.Registry.MobEffect> GLOWING = Register("glowing", new MobEffect(MobEffectCategory.Neutral, 9740385));
    public static readonly Holder<NetCraft.Registry.MobEffect> LEVITATION = Register("levitation", new MobEffect(MobEffectCategory.Harmful, 0xCEFFFF));
    public static readonly Holder<NetCraft.Registry.MobEffect> LUCK = Register("luck", new MobEffect(MobEffectCategory.Beneficial, 5882118));
    public static readonly Holder<NetCraft.Registry.MobEffect> UNLUCK = Register("unluck", new MobEffect(MobEffectCategory.Harmful, 12624973));
    public static readonly Holder<NetCraft.Registry.MobEffect> SLOW_FALLING = Register("slow_falling", new MobEffect(MobEffectCategory.Beneficial, 15978425));
    public static readonly Holder<NetCraft.Registry.MobEffect> CONDUIT_POWER = Register("conduit_power", new MobEffect(MobEffectCategory.Beneficial, 1950417));
    public static readonly Holder<NetCraft.Registry.MobEffect> DOLPHINS_GRACE = Register("dolphins_grace", new MobEffect(MobEffectCategory.Beneficial, 8954814));
    public static readonly Holder<NetCraft.Registry.MobEffect> BAD_OMEN = Register("bad_omen", new MobEffect(MobEffectCategory.Neutral, 745784));
    public static readonly Holder<NetCraft.Registry.MobEffect> HERO_OF_THE_VILLAGE = Register("hero_of_the_village", new MobEffect(MobEffectCategory.Beneficial, 0x44FF44));
    public static readonly Holder<NetCraft.Registry.MobEffect> DARKNESS = Register("darkness", new MobEffect(MobEffectCategory.Harmful, 2696993).SetBlendDuration(22));
    public static readonly Holder<NetCraft.Registry.MobEffect> TRIAL_OMEN = Register("trial_omen", new MobEffect(MobEffectCategory.Neutral, 0x16A6A6));
    public static readonly Holder<NetCraft.Registry.MobEffect> RAID_OMEN = Register("raid_omen", new MobEffect(MobEffectCategory.Neutral, 14565464));
    public static readonly Holder<NetCraft.Registry.MobEffect> WIND_CHARGED = Register("wind_charged", new MobEffect(MobEffectCategory.Harmful, 12438015));
    public static readonly Holder<NetCraft.Registry.MobEffect> WEAVING = Register("weaving", new MobEffect(MobEffectCategory.Harmful, 7891290));
    public static readonly Holder<NetCraft.Registry.MobEffect> OOZING = Register("oozing", new MobEffect(MobEffectCategory.Harmful, 10092451));
    public static readonly Holder<NetCraft.Registry.MobEffect> INFESTED = Register("infested", new MobEffect(MobEffectCategory.Harmful, 9214860));
    public static readonly Holder<NetCraft.Registry.MobEffect> BREATH_OF_THE_NAUTILUS = Register("breath_of_the_nautilus", new MobEffect(MobEffectCategory.Beneficial, 65518));

    //Bootstrap 触发内置效果登记 幂等
    //登记实际由字段初始化器在类型首次访问时按声明顺序完成 这里只做幂等标记
    public static void Bootstrap()
    {
        if (_bootstrapped) return;
        _bootstrapped = true;
    }

    //Find 按注册名取效果实现 未注册或非本子系统实现返回 null
    public static MobEffect? Find(Identifier id)
        => BuiltInRegistries.MOB_EFFECT.GetValue(id) as MobEffect;

    //Register 按调用顺序登记 注册表 id 由登记顺序决定
    private static Holder<NetCraft.Registry.MobEffect> Register(string path, NetCraft.Registry.MobEffect value)
    {
        var id = Identifier.WithDefaultNamespace(path);
        var key = ResourceKey<NetCraft.Registry.MobEffect>.Create(Registries.MOB_EFFECT, id);
        return Registry<NetCraft.Registry.MobEffect>.RegisterForHolder(BuiltInRegistries.MOB_EFFECT, key, value);
    }
}
