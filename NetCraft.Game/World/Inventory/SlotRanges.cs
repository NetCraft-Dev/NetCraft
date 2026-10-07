namespace NetCraft.Game.World.Inventory;

//SlotRanges maps slot names to slot indices, maps to vanilla net.minecraft.world.inventory.SlotRanges
//Names and values are copied from vanilla's initialization order, the command slot parsing and completion both read this table
//This project only has the player inventory, 0..40 are actually writable; names like ender chest / riding / mob inventory are kept as-is
//So that parsing and completion match vanilla; whether they resolve to an entity is decided by SlotAccess
public static class SlotRanges
{
    //MainHand main hand, vanilla EquipmentSlot.MAINHAND.getIndex(98) = 98 + 0
    public const int MainHand = 98;
    //OffHand offhand, vanilla EquipmentSlot.OFFHAND.getIndex(98) = 98 + 5
    //Note it shares a value with Chest, as in vanilla; reverse lookup hits OFFHAND first per EquipmentSlot declaration order
    public const int OffHand = 103;
    //Feet/Legs armor, vanilla EquipmentSlot.FEET/LEGS.getIndex(100) = 100 + 1/2
    public const int Feet = 101;
    public const int Legs = 102;
    //Chest chestplate, vanilla EquipmentSlot.CHEST.getIndex(100) = 100 + 3, same value as offhand
    public const int Chest = 103;
    //Head helmet, vanilla EquipmentSlot.HEAD.getIndex(100) = 100 + 4
    public const int Head = 104;
    //Body animal armor, vanilla EquipmentSlot.BODY.getIndex(105) = 105 + 6
    public const int Body = 111;
    //Saddle saddle, vanilla EquipmentSlot.SADDLE.getIndex(106) = 106 + 7
    public const int Saddle = 113;

    private static readonly Dictionary<string, int[]> Ranges;
    private static readonly string[] SingleNames;

    static SlotRanges()
    {
        var ranges = new Dictionary<string, int[]>();
        var singles = new List<string>();

        //Single registers a single-slot name and adds it to the completion list, distinct from multi-slot names
        void Single(string name, int id)
        {
            ranges[name] = new[] { id };
            singles.Add(name);
        }

        //Range registers a whole range like inventory.0..26 and also adds inventory.* to cover it
        void Range(string prefix, int offset, int size)
        {
            var ids = new int[size];
            for (var i = 0; i < size; i++)
            {
                ids[i] = offset + i;
                ranges[prefix + i] = new[] { offset + i };
                singles.Add(prefix + i);
            }
            ranges[prefix + "*"] = ids;
        }

        Single("contents", 0);
        Range("container.", 0, 54);
        Range("hotbar.", 0, 9);
        Range("inventory.", 9, 27);
        Range("enderchest.", 200, 27);
        Range("mob.inventory.", 300, 8);
        Range("horse.", 500, 15);
        Single("weapon", MainHand);
        Single("weapon.mainhand", MainHand);
        Single("weapon.offhand", OffHand);
        ranges["weapon.*"] = new[] { MainHand, OffHand };
        Single("armor.head", Head);
        Single("armor.chest", Chest);
        Single("armor.legs", Legs);
        Single("armor.feet", Feet);
        Single("armor.body", Body);
        ranges["armor.*"] = new[] { Head, Chest, Legs, Feet, Body };
        Single("saddle", Saddle);
        Single("horse.chest", 499);
        Single("player.cursor", 499);
        Range("player.crafting.", 500, 4);

        Ranges = ranges;
        SingleNames = singles.ToArray();
    }

    //NameToIds resolves slot indices by name, returns null for unknown names, maps to vanilla nameToIds
    public static int[]? NameToIds(string name)
        => Ranges.TryGetValue(name, out var ids) ? ids : null;

    //SingleSlotNames list of single-slot names in registration order, used for item_slot argument completion
    public static IReadOnlyList<string> SingleSlotNames() => SingleNames;
}
