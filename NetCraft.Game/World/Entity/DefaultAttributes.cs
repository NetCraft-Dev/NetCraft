using NetCraft.Registry;
using AttributeSupplier = NetCraft.Registry.EntityAttribute.AttributeSupplier;
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//DefaultAttributes map from entity type to its default attribute table, maps to vanilla DefaultAttributes
//Layering follows vanilla: living base -> mob -> animal/monster -> specific species, each layer builds on the previous
//Both sides share the same table; client entities look it up by type and already have initial attributes locally, the server only resends when attributes were changed
public static class DefaultAttributes
{
    //Suppliers entity type to default table, the key is the same type instance in the registry
    private static readonly Dictionary<EntityType<object>, AttributeSupplier> Suppliers = new();
    private static bool _bootstrapped;

    //GetSupplier returns the attribute default table of an entity type, null when the type has no attributes, maps to vanilla getSupplier
    public static AttributeSupplier? GetSupplier(EntityType<object> type) => Suppliers.GetValueOrDefault(type);

    //HasSupplier whether the type has registered attributes, maps to vanilla hasSupplier
    public static bool HasSupplier(EntityType<object> type) => Suppliers.ContainsKey(type);

    //Bootstrap wires up the default tables of built-in types, must run before any entity is constructed, idempotent
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

    //CreateLivingAttributes living base table, maps to vanilla LivingEntity.createLivingAttributes
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

    //CreateMobAttributes mob table, the living base plus follow range 16, maps to vanilla Mob.createMobAttributes
    private static AttributeSupplier.Builder CreateMobAttributes()
        => CreateLivingAttributes().Add(EntityAttributes.FollowRange, 16.0);

    //CreateAnimalAttributes animal table, the mob table plus temptation range 10, maps to vanilla Animal.createAnimalAttributes
    private static AttributeSupplier.Builder CreateAnimalAttributes()
        => CreateMobAttributes().Add(EntityAttributes.TemptRange, 10.0);

    //CreateMonsterAttributes monster table, the mob table plus attack damage, maps to vanilla Monster.createMonsterAttributes
    private static AttributeSupplier.Builder CreateMonsterAttributes()
        => CreateMobAttributes().Add(EntityAttributes.AttackDamage);

    //CreatePlayerAttributes player table, maps to vanilla Player.createAttributes
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
