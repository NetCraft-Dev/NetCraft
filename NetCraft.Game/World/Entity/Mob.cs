using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.EntityAttribute;
//The attribute constants class shares a name with the Attributes instance property on Entity, so referencing the constants needs an alias
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//Mob mob entity, maps to vanilla net.minecraft.world.entity.Mob
//Extends Entity and holds an entity type reference and AI flags
//Concrete mob classes (zombie/pig etc.) are added as needed once AI/pathfinding lands; for now the type factory builds Mob directly
public class Mob : LivingEntity
{
    private readonly EntityType<object> _type;

    //Mob constructor, the entity type is passed at registry registration and determines the registry name and network index
    //The attribute map is looked up by type from DefaultAttributes, all species differences live there, an unregistered type falls back to an empty map
    public Mob(EntityType<object> type)
    {
        _type = type;
        SetAttributes(new AttributeMap(DefaultAttributes.GetSupplier(type) ?? AttributeSupplier.Empty));
    }

    //A living entity's gravity/knockback resistance/safe fall distance/fall damage multiplier/step height all go through attributes
    //Maps to the points where vanilla LivingEntity overrides Entity and reads attributes
    public override double DefaultGravity => GetAttributeValue(EntityAttributes.Gravity);
    public override double KnockbackResistance => GetAttributeValue(EntityAttributes.KnockbackResistance);
    public override double SafeFallDistance => GetAttributeValue(EntityAttributes.SafeFallDistance);
    public override double FallDamageMultiplier => GetAttributeValue(EntityAttributes.FallDamageMultiplier);
    public override double MaxUpStep => GetAttributeValue(EntityAttributes.StepHeight);

    //Id entity registry name taken from the bound type
    public override Identifier Id => _type.Id;

    //Type entity type, the tracker uses it to get the tracking range and network index
    public override EntityType<object>? Type => _type;

    //A mob is a living entity and blocks block placement, maps to blocksBuilding being enabled in the vanilla LivingEntity constructor
    public override bool BlocksBuilding => true;

    //A mob is a living entity and takes fall damage, maps to vanilla LivingEntity overriding Entity
    public override bool TakesFallDamage => true;

    //NoAi whether AI is disabled, default false, aligned with the vanilla NoAI NBT tag
    public bool NoAi { get; set; }

    //TargetUuid UUID of the current attack target, a placeholder until the AI subsystem is ready
    public Guid? TargetUuid { get; set; }

    //PersistenceRequired whether to force persistence without unloading, default false
    public bool PersistenceRequired { get; set; }

    protected override void AddAdditionalSaveData(CompoundTag tag)
    {
        base.AddAdditionalSaveData(tag);
        tag.PutBoolean("NoAI", NoAi);
        tag.PutBoolean("PersistenceRequired", PersistenceRequired);
    }

    protected override void ReadAdditionalSaveData(CompoundTag tag)
    {
        base.ReadAdditionalSaveData(tag);
        NoAi = tag.GetBooleanOr("NoAI", false);
        PersistenceRequired = tag.GetBooleanOr("PersistenceRequired", false);
    }
}
