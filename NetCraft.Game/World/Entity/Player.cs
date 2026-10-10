using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.EntityAttribute;
//The attribute constants class shares a name with the Attributes instance property on Entity, so referencing the constants needs an alias
using EntityAttributes = NetCraft.Registry.EntityAttribute.Attributes;

namespace NetCraft.Game.World.Entity;

//Inventory player inventory PoC data layer, maps to vanilla net.minecraft.world.entity.player.Inventory
//The PoC identifies items by object identity (e.g. the string "cube") and does not use the real ItemStack/Item registry
//In production, network packets such as ClientboundContainerSetContentPacket fill in real ItemStacks
//Hotbar takes slots 0-8, the main inventory slots 9-35, 36 slots total matching vanilla
public sealed class Inventory
{
    //HotbarSlots fixed hotbar slot count of 9, matching vanilla
    public const int HotbarSlots = 9;
    //TotalSlots total slot count, 36 = 9 hotbar + 27 main inventory, matching vanilla
    public const int TotalSlots = 36;

    //_items slot item identities, null means an empty slot
    private readonly object?[] _items = new object?[TotalSlots];

    //GetItem returns the item identity in the given slot, null means empty
    public object? GetItem(int slot) => (uint)slot < TotalSlots ? _items[slot] : null;

    //SetItem sets the item identity in the given slot, null clears it
    public void SetItem(int slot, object? identity)
    {
        if ((uint)slot < TotalSlots) _items[slot] = identity;
    }

    //GetHotbarItem returns the hotbar slot item identity (0-8), a convenience for GameScreen hotbar rendering
    public object? GetHotbarItem(int hotbarSlot) => GetItem(hotbarSlot);
}

//Player player entity, maps to vanilla net.minecraft.world.entity.player.Player
//Extends Entity and holds the core experience/health/hunger fields
//Subsystems such as Inventory/Abilities come later, only basic fields are here
public class Player : LivingEntity
{
    //Id the player entity type registry name is fixed to minecraft:player
    public override Identifier Id => Identifier.WithDefaultNamespace("player");

    //Type player entity type, the tracker uses it to recognize players
    public override EntityType<object>? Type => EntityTypes.PLAYER;

    //A player is a living entity and blocks block placement, maps to blocksBuilding being enabled in the vanilla LivingEntity constructor
    public override bool BlocksBuilding => true;

    //A player is a living entity and takes fall damage, maps to vanilla LivingEntity overriding Entity
    public override bool TakesFallDamage => true;

    //A player's gravity/knockback resistance/safe fall distance/fall damage multiplier/step height all go through attributes
    public override double DefaultGravity => GetAttributeValue(EntityAttributes.Gravity);
    public override double KnockbackResistance => GetAttributeValue(EntityAttributes.KnockbackResistance);
    public override double SafeFallDistance => GetAttributeValue(EntityAttributes.SafeFallDistance);
    public override double FallDamageMultiplier => GetAttributeValue(EntityAttributes.FallDamageMultiplier);
    public override double MaxUpStep => GetAttributeValue(EntityAttributes.StepHeight);

    //XpLevel player experience level, default 0
    public int XpLevel { get; set; }

    //XpP current experience progress, 0 to 1
    public float XpP { get; set; }

    //XpTotal total accumulated experience points
    public int XpTotal { get; set; }

    //FoodLevel hunger value, default 20
    public int FoodLevel { get; set; } = 20;

    //AbsorptionHealth absorption health (golden apple etc.), default 0, used to render absorption hearts
    public float AbsorptionHealth { get; set; }

    //ActiveEffects current active potion effect set, used by forPlayer to detect POISON/WITHER/REGENERATION
    public HashSet<MobEffect> ActiveEffects { get; set; } = new();

    //IsFullyFrozen whether the player is fully frozen (powder snow etc.), used by forPlayer to detect FROZEN
    public bool IsFullyFrozen { get; set; }

    //IsHardcore hardcore mode, used to pick the hardcore heart sprite variant, matching vanilla level.getLevelData().isHardcore()
    public bool IsHardcore { get; set; }

    //GameMode 0=survival 1=creative 2=adventure 3=spectator
    public int GameMode { get; set; }

    //Inventory player inventory PoC data layer, hotbar 0-8, main inventory 9-35
    //Production fills it from network packets; the PoC pre-fills cube to demo hotbar item icon rendering
    public Inventory Inventory { get; } = new();

    public Player()
    {
        //The player attribute map is taken by type, maps to vanilla Player.createAttributes
        SetAttributes(new AttributeMap(DefaultAttributes.GetSupplier(EntityTypes.PLAYER) ?? AttributeSupplier.Empty));
        //The player stands slightly above the origin by default
        Pos = new Vec3(0, 0, 0);
        //The PoC pre-fills the 9 hotbar slots with cube items to demo hotbar item icon rendering
        //In production, ClientboundContainerSetContentPacket fills in real ItemStacks
        for (var i = 0; i < Inventory.HotbarSlots; i++)
            Inventory.SetItem(i, "cube");
    }
}
