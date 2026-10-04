using NetCraft.Registry;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//DefaultAttributes 实体类型到属性默认表的映射 对应原版 DefaultAttributes
//分层照原版走 活体基础表 → 生物表 → 动物/怪物表 → 具体物种表 每层在上一层上叠加
//两端共用同一份表 客户端实体按类型取表 本地就有初始属性 服务端只在属性被改过时补发
public static class DefaultAttributes
{
    //Suppliers 实体类型到默认表 键是注册表里的同一个类型实例
    private static readonly Dictionary<EntityType<object>, AttributeSupplier> Suppliers = new();
    private static bool _bootstrapped;

    //GetSupplier 取实体类型的属性默认表 该类型没有属性返回 null 对应原版 getSupplier
    public static AttributeSupplier? GetSupplier(EntityType<object> type) => Suppliers.GetValueOrDefault(type);

    //HasSupplier 该类型是否登记过属性 对应原版 hasSupplier
    public static bool HasSupplier(EntityType<object> type) => Suppliers.ContainsKey(type);

    //Bootstrap 装配内置类型的默认表 必须早于任何实体构造 幂等
    public static void Bootstrap()
    {
        if (_bootstrapped) return;
        _bootstrapped = true;
        Suppliers[EntityTypes.PIG] = CreateAnimalAttributes()
            .Add(EntityAttributes.MaxHealth, 10.0)
            .Add(EntityAttributes.MovementSpeed, 0.25).Build();
        Suppliers[EntityTypes.COW] = CreateAnimalAttributes()
            .Add(EntityAttributes.MaxHealth, 10.0)
            .Add(EntityAttributes.MovementSpeed, 0.2).Build();
        Suppliers[EntityTypes.CHICKEN] = CreateAnimalAttributes()
            .Add(EntityAttributes.MaxHealth, 4.0)
            .Add(EntityAttributes.MovementSpeed, 0.25).Build();
        Suppliers[EntityTypes.ZOMBIE] = CreateMonsterAttributes()
            .Add(EntityAttributes.FollowRange, 35.0)
            .Add(EntityAttributes.MovementSpeed, 0.23)
            .Add(EntityAttributes.AttackDamage, 3.0)
            .Add(EntityAttributes.Armor, 2.0)
            .Add(EntityAttributes.SpawnReinforcements).Build();
        Suppliers[EntityTypes.PLAYER] = CreatePlayerAttributes().Build();
    }

    //CreateLivingAttributes 活体基础表 对应原版 LivingEntity.createLivingAttributes
    private static AttributeSupplier.Builder CreateLivingAttributes()
        => AttributeSupplier.Builder.Create()
            .Add(EntityAttributes.MaxHealth)
            .Add(EntityAttributes.KnockbackResistance)
            .Add(EntityAttributes.MovementSpeed)
            .Add(EntityAttributes.Armor)
            .Add(EntityAttributes.ArmorToughness)
            .Add(EntityAttributes.MaxAbsorption)
            .Add(EntityAttributes.StepHeight)
            .Add(EntityAttributes.Scale)
            .Add(EntityAttributes.Gravity)
            .Add(EntityAttributes.SafeFallDistance)
            .Add(EntityAttributes.FallDamageMultiplier)
            .Add(EntityAttributes.JumpStrength)
            .Add(EntityAttributes.EntityInteractionRange)
            .Add(EntityAttributes.OxygenBonus)
            .Add(EntityAttributes.BurningTime)
            .Add(EntityAttributes.ExplosionKnockbackResistance)
            .Add(EntityAttributes.WaterMovementEfficiency)
            .Add(EntityAttributes.MovementEfficiency)
            .Add(EntityAttributes.AttackKnockback)
            .Add(EntityAttributes.CameraDistance)
            .Add(EntityAttributes.WaypointTransmitRange)
            .Add(EntityAttributes.Bounciness)
            .Add(EntityAttributes.AirDragModifier)
            .Add(EntityAttributes.FrictionModifier)
            .Add(EntityAttributes.NameTagDistance)
            .Add(EntityAttributes.BelowNameDistance);

    //CreateMobAttributes 生物表 活体基础表加跟随距离 16 对应原版 Mob.createMobAttributes
    private static AttributeSupplier.Builder CreateMobAttributes()
        => CreateLivingAttributes().Add(EntityAttributes.FollowRange, 16.0);

    //CreateAnimalAttributes 动物表 生物表加引诱距离 10 对应原版 Animal.createAnimalAttributes
    private static AttributeSupplier.Builder CreateAnimalAttributes()
        => CreateMobAttributes().Add(EntityAttributes.TemptRange, 10.0);

    //CreateMonsterAttributes 怪物表 生物表加攻击伤害 对应原版 Monster.createMonsterAttributes
    private static AttributeSupplier.Builder CreateMonsterAttributes()
        => CreateMobAttributes().Add(EntityAttributes.AttackDamage);

    //CreatePlayerAttributes 玩家表 对应原版 Player.createAttributes
    private static AttributeSupplier.Builder CreatePlayerAttributes()
        => CreateLivingAttributes()
            .Add(EntityAttributes.AttackDamage, 1.0)
            .Add(EntityAttributes.MovementSpeed, 0.1)
            .Add(EntityAttributes.AttackSpeed, EntityAttributes.DefaultAttackSpeed)
            .Add(EntityAttributes.Luck)
            .Add(EntityAttributes.BlockInteractionRange)
            .Add(EntityAttributes.BlockBreakSpeed)
            .Add(EntityAttributes.SubmergedMiningSpeed)
            .Add(EntityAttributes.SneakingSpeed)
            .Add(EntityAttributes.MiningEfficiency)
            .Add(EntityAttributes.SweepingDamageRatio)
            .Add(EntityAttributes.WaypointTransmitRange, 6.0E7)
            .Add(EntityAttributes.WaypointReceiveRange, 6.0E7);
}
