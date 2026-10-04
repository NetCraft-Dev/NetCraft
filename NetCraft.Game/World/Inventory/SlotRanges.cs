namespace NetCraft.Game.World.Inventory;

//SlotRanges 槽位名到槽位号的映射 对应原版 net.minecraft.world.inventory.SlotRanges
//名字与数值照原版初始化顺序抄 命令的槽位解析与补全都读这张表
//本项目只有玩家背包 0..40 是真实可写的 末影箱/骑马/生物背包这些名字原样保留
//为的是解析与补全和原版一致 具体能不能落到实体上由 SlotAccess 决定
public static class SlotRanges
{
    //MainHand 主手 原版 EquipmentSlot.MAINHAND.getIndex(98) = 98 + 0
    public const int MainHand = 98;
    //OffHand 副手 原版 EquipmentSlot.OFFHAND.getIndex(98) = 98 + 5
    //注意它与 Chest 同值 原版也是同值 反查按 EquipmentSlot 声明序先命中 OFFHAND
    public const int OffHand = 103;
    //Feet/Legs 护甲 原版 EquipmentSlot.FEET/LEGS.getIndex(100) = 100 + 1/2
    public const int Feet = 101;
    public const int Legs = 102;
    //Chest 胸甲 原版 EquipmentSlot.CHEST.getIndex(100) = 100 + 3 与副手同值
    public const int Chest = 103;
    //Head 头盔 原版 EquipmentSlot.HEAD.getIndex(100) = 100 + 4
    public const int Head = 104;
    //Body 动物盔甲 原版 EquipmentSlot.BODY.getIndex(105) = 105 + 6
    public const int Body = 111;
    //Saddle 鞍 原版 EquipmentSlot.SADDLE.getIndex(106) = 106 + 7
    public const int Saddle = 113;

    private static readonly Dictionary<string, int[]> Ranges;
    private static readonly string[] SingleNames;

    static SlotRanges()
    {
        var ranges = new Dictionary<string, int[]>();
        var singles = new List<string>();

        //Single 登记单槽名 同时进补全名单 与多槽名区分
        void Single(string name, int id)
        {
            ranges[name] = new[] { id };
            singles.Add(name);
        }

        //Range 登记形如 inventory.0..26 的整段 另外补一个 inventory.* 覆盖整段
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

    //NameToIds 按名字取槽位号 未知名字返回 null 对应原版 nameToIds
    public static int[]? NameToIds(string name)
        => Ranges.TryGetValue(name, out var ids) ? ids : null;

    //SingleSlotNames 单槽名列表 顺序与登记顺序一致 供 item_slot 参数补全
    public static IReadOnlyList<string> SingleSlotNames() => SingleNames;
}
