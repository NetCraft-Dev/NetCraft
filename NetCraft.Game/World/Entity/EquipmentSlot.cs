namespace NetCraft.Game.World.Entity;

//EquipmentSlot equipment slot, maps to vanilla net.minecraft.world.entity.EquipmentSlot
//8 slots, an enum whose numeric values align with vanilla ids to simplify VarInt encoding
//The vanilla fields type/index/countLimit/name are queried through extension methods
public enum EquipmentSlot
{
    MAINHAND = 0,
    FEET = 1,
    LEGS = 2,
    CHEST = 3,
    HEAD = 4,
    OFFHAND = 5,
    BODY = 6,
    SADDLE = 7
}

//EquipmentSlotType equipment slot type, maps to vanilla EquipmentSlot.Type
public enum EquipmentSlotType
{
    HAND,
    HUMANOID_ARMOR,
    ANIMAL_ARMOR,
    SADDLE
}

//EquipmentSlotExtensions equipment slot extension methods
//Provides name/type/index/countLimit/byId/byName lookups matching vanilla
public static class EquipmentSlotExtensions
{
    //GetSlotType returns the equipment slot type, matches vanilla getType
    public static EquipmentSlotType GetSlotType(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MAINHAND or EquipmentSlot.OFFHAND => EquipmentSlotType.HAND,
        EquipmentSlot.FEET or EquipmentSlot.LEGS or EquipmentSlot.CHEST or EquipmentSlot.HEAD => EquipmentSlotType.HUMANOID_ARMOR,
        EquipmentSlot.BODY => EquipmentSlotType.ANIMAL_ARMOR,
        EquipmentSlot.SADDLE => EquipmentSlotType.SADDLE,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    //GetName returns the lowercase slot name, matches vanilla getName/getSerializedName
    public static string GetName(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MAINHAND => "mainhand",
        EquipmentSlot.OFFHAND => "offhand",
        EquipmentSlot.FEET => "feet",
        EquipmentSlot.LEGS => "legs",
        EquipmentSlot.CHEST => "chest",
        EquipmentSlot.HEAD => "head",
        EquipmentSlot.BODY => "body",
        EquipmentSlot.SADDLE => "saddle",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    //GetIndex returns the index within the same type, matches vanilla getIndex
    public static int GetIndex(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MAINHAND => 0,
        EquipmentSlot.OFFHAND => 1,
        _ => 0
    };

    //GetCountLimit returns the stack limit, matches vanilla countLimit
    //Armor slots return 1, hand slots return 0 meaning unlimited
    public static int GetCountLimit(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.FEET or EquipmentSlot.LEGS or EquipmentSlot.CHEST or EquipmentSlot.HEAD or EquipmentSlot.BODY or EquipmentSlot.SADDLE => 1,
        _ => 0
    };

    //GetId returns the vanilla id, aligned with the enum numeric value
    public static int GetId(this EquipmentSlot slot) => (int)slot;

    //ById looks up by id matching vanilla BY_ID, falls back to MAINHAND when out of range
    public static EquipmentSlot ById(int id) => id >= 0 && id <= 7 ? (EquipmentSlot)id : EquipmentSlot.MAINHAND;

    //ByName looks up by name matching vanilla byName
    public static EquipmentSlot ByName(string name) => name switch
    {
        "mainhand" => EquipmentSlot.MAINHAND,
        "offhand" => EquipmentSlot.OFFHAND,
        "feet" => EquipmentSlot.FEET,
        "legs" => EquipmentSlot.LEGS,
        "chest" => EquipmentSlot.CHEST,
        "head" => EquipmentSlot.HEAD,
        "body" => EquipmentSlot.BODY,
        "saddle" => EquipmentSlot.SADDLE,
        _ => throw new ArgumentException($"Invalid slot '{name}'")
    };

    //IsArmor whether it is an armor slot, matches vanilla isArmor
    public static bool IsArmor(this EquipmentSlot slot)
    {
        var type = slot.GetSlotType();
        return type == EquipmentSlotType.HUMANOID_ARMOR || type == EquipmentSlotType.ANIMAL_ARMOR;
    }
}
