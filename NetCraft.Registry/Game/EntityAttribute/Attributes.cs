namespace NetCraft.Registry.EntityAttribute;

//Attributes 实体属性条目 对应原版 net.minecraft.world.entity.ai.attributes.Attributes
//原版字段是 Holder<Attribute> 这里直接用属性实例 注册表里 id 与实例一一对应
//字段只是属性实例 归属注册表的登记动作在 Bootstrap 里做 必须早于注册表冻结
public static class Attributes
{
    //DefaultAttackSpeed 默认攻击速度 对应原版同名常量
    public const double DefaultAttackSpeed = 4.0;

    //Pending 待登记项 静态字段按声明顺序灌进来 Bootstrap 统一写注册表
    private static readonly List<(string Id, Attribute Attribute)> Pending = new();

    private static bool _bootstrapped;

    public static readonly Attribute AirDragModifier = Ranged("air_drag_modifier", 1.0, 0.0, 2048.0, true);
    public static readonly Attribute Armor = Ranged("armor", 0.0, 0.0, 30.0, true);
    public static readonly Attribute ArmorToughness = Ranged("armor_toughness", 0.0, 0.0, 20.0, true);
    public static readonly Attribute AttackDamage = Ranged("attack_damage", 2.0, 0.0, 2048.0);
    public static readonly Attribute AttackKnockback = Ranged("attack_knockback", 0.0, 0.0, 5.0);
    public static readonly Attribute AttackSpeed = Ranged("attack_speed", 4.0, 0.0, 1024.0, true);
    public static readonly Attribute BelowNameDistance = Ranged("below_name_distance", 10.0, 0.0, 512.0, true);
    public static readonly Attribute BlockBreakSpeed = Ranged("block_break_speed", 1.0, 0.0, 1024.0, true);
    public static readonly Attribute BlockInteractionRange = Ranged("block_interaction_range", 4.5, 0.0, 64.0, true);
    public static readonly Attribute Bounciness = Ranged("bounciness", 0.0, 0.0, 1.0, true);
    public static readonly Attribute BurningTime = Ranged("burning_time", 1.0, 0.0, 1024.0, true,
        Attribute.Sentiment.Negative);
    public static readonly Attribute CameraDistance = Ranged("camera_distance", 4.0, 0.0, 32.0, true);
    public static readonly Attribute ExplosionKnockbackResistance =
        Ranged("explosion_knockback_resistance", 0.0, 0.0, 1.0, true);
    public static readonly Attribute EntityInteractionRange = Ranged("entity_interaction_range", 3.0, 0.0, 64.0, true);
    public static readonly Attribute FallDamageMultiplier = Ranged("fall_damage_multiplier", 1.0, 0.0, 100.0, true,
        Attribute.Sentiment.Negative);
    public static readonly Attribute FlyingSpeed = Ranged("flying_speed", 0.4, 0.0, 1024.0, true);
    public static readonly Attribute FollowRange = Ranged("follow_range", 32.0, 0.0, 2048.0);
    public static readonly Attribute FrictionModifier = Ranged("friction_modifier", 1.0, 0.0, 2048.0, true);
    public static readonly Attribute Gravity = Ranged("gravity", 0.08, -1.0, 1.0, true, Attribute.Sentiment.Neutral);
    public static readonly Attribute JumpStrength =
        Ranged("jump_strength", 0.41999998688697815, 0.0, 32.0, true);
    public static readonly Attribute KnockbackResistance = Ranged("knockback_resistance", 0.0, -2.0, 1.0);
    public static readonly Attribute Luck = Ranged("luck", 0.0, -1024.0, 1024.0, true);
    public static readonly Attribute MaxAbsorption = Ranged("max_absorption", 0.0, 0.0, 2048.0, true);
    public static readonly Attribute MaxHealth = Ranged("max_health", 20.0, 1.0, 1024.0, true);
    public static readonly Attribute MiningEfficiency = Ranged("mining_efficiency", 0.0, 0.0, 1024.0, true);
    public static readonly Attribute MovementEfficiency = Ranged("movement_efficiency", 0.0, 0.0, 1.0, true);
    public static readonly Attribute MovementSpeed = Ranged("movement_speed", 0.7, 0.0, 1024.0, true);
    public static readonly Attribute NameTagDistance = Ranged("name_tag_distance", 64.0, 0.0, 512.0, true);
    public static readonly Attribute OxygenBonus = Ranged("oxygen_bonus", 0.0, 0.0, 1024.0, true);
    public static readonly Attribute SafeFallDistance = Ranged("safe_fall_distance", 3.0, -1024.0, 1024.0, true);
    public static readonly Attribute Scale = Ranged("scale", 1.0, 0.0625, 16.0, true, Attribute.Sentiment.Neutral);
    public static readonly Attribute SneakingSpeed = Ranged("sneaking_speed", 0.3, 0.0, 1.0, true);
    public static readonly Attribute SpawnReinforcements = Ranged("spawn_reinforcements", 0.0, 0.0, 1.0);
    public static readonly Attribute StepHeight = Ranged("step_height", 0.6, 0.0, 10.0, true);
    public static readonly Attribute SubmergedMiningSpeed = Ranged("submerged_mining_speed", 0.2, 0.0, 20.0, true);
    public static readonly Attribute SweepingDamageRatio = Ranged("sweeping_damage_ratio", 0.0, 0.0, 1.0, true);
    public static readonly Attribute TemptRange = Ranged("tempt_range", 10.0, 0.0, 2048.0);
    public static readonly Attribute WaterMovementEfficiency =
        Ranged("water_movement_efficiency", 0.0, 0.0, 1.0, true);
    public static readonly Attribute WaypointTransmitRange =
        Ranged("waypoint_transmit_range", 0.0, 0.0, 6.0E7, false, Attribute.Sentiment.Neutral);
    public static readonly Attribute WaypointReceiveRange =
        Ranged("waypoint_receive_range", 0.0, 0.0, 6.0E7, false, Attribute.Sentiment.Neutral);

    //Bootstrap 把全部属性登记进注册表 对应原版 Attributes.bootstrap
    //访问本方法会先触发静态构造 全部字段按声明顺序灌进 Pending 再统一注册
    public static void Bootstrap()
    {
        if (_bootstrapped) return;
        _bootstrapped = true;
        foreach (var (id, attribute) in Pending)
            Registry<Attribute>.Register(BuiltInRegistries.ATTRIBUTE, Identifier.WithDefaultNamespace(id), attribute);
    }

    //Ranged 建一条带区间的属性 描述键按原版拼 attribute.name.<id>
    private static Attribute Ranged(string id, double defaultValue, double minValue, double maxValue,
        bool syncable = false, Attribute.Sentiment sentiment = Attribute.Sentiment.Positive)
    {
        var attribute = new RangedAttribute($"attribute.name.{id}", defaultValue, minValue, maxValue);
        if (syncable) attribute.SetSyncable(true);
        if (sentiment != Attribute.Sentiment.Positive) attribute.SetSentiment(sentiment);
        Pending.Add((id, attribute));
        return attribute;
    }
}
