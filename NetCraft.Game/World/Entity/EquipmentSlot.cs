namespace NetCraft.Game.World.Entity;

//EquipmentSlot 装备槽对应原版 net.minecraft.world.entity.EquipmentSlot
//8 个槽位enum 数字值对齐原版 id 简化 VarInt 编解码
//原版字段 type/index/countLimit/name 通过扩展方法查询
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

//EquipmentSlotType 装备槽类型对应原版 EquipmentSlot.Type
public enum EquipmentSlotType
{
    HAND,
    HUMANOID_ARMOR,
    ANIMAL_ARMOR,
    SADDLE
}

//EquipmentSlotExtensions 装备槽扩展方法
//提供 name/type/index/countLimit/byId/byName 查询对齐原版
public static class EquipmentSlotExtensions
{
    //GetSlotType 返回装备槽类型对齐原版 getType
    public static EquipmentSlotType GetSlotType(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MAINHAND or EquipmentSlot.OFFHAND => EquipmentSlotType.HAND,
        EquipmentSlot.FEET or EquipmentSlot.LEGS or EquipmentSlot.CHEST or EquipmentSlot.HEAD => EquipmentSlotType.HUMANOID_ARMOR,
        EquipmentSlot.BODY => EquipmentSlotType.ANIMAL_ARMOR,
        EquipmentSlot.SADDLE => EquipmentSlotType.SADDLE,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    //GetName 返回槽位小写名字符串对齐原版 getName/getSerializedName
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

    //GetIndex 返回同类型内的索引对齐原版 getIndex
    public static int GetIndex(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MAINHAND => 0,
        EquipmentSlot.OFFHAND => 1,
        _ => 0
    };

    //GetCountLimit 返回堆叠上限对齐原版 countLimit
    //盔槽类返回 1 手槽返回 0 表示无限制
    public static int GetCountLimit(this EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.FEET or EquipmentSlot.LEGS or EquipmentSlot.CHEST or EquipmentSlot.HEAD or EquipmentSlot.BODY or EquipmentSlot.SADDLE => 1,
        _ => 0
    };

    //GetId 返回原版 id 对齐 enum 数字值
    public static int GetId(this EquipmentSlot slot) => (int)slot;

    //ById 按 id 查询对齐原版 BY_ID 越界回退 MAINHAND
    public static EquipmentSlot ById(int id) => id >= 0 && id <= 7 ? (EquipmentSlot)id : EquipmentSlot.MAINHAND;

    //ByName 按名字查询对齐原版 byName
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

    //IsArmor 是否盔槽对齐原版 isArmor
    public static bool IsArmor(this EquipmentSlot slot)
    {
        var type = slot.GetSlotType();
        return type == EquipmentSlotType.HUMANOID_ARMOR || type == EquipmentSlotType.ANIMAL_ARMOR;
    }
}
