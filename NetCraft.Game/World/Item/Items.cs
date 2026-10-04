using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//Items 内置物品常量对应原版 net.minecraft.world.item.Items
//注册到 BuiltInRegistries.ITEM 注册表
//按 VanillaItems.Order 原版注册顺序逐项填充 保证 ITEM 注册表 id 等于原版 id
//客户端按本地 vanilla ITEM 表解析 ItemStack 序号 序号错位会显示成别的物品
public static class Items
{
    //BasicItem 普通物品只有注册名
    private sealed class BasicItem : Item
    {
        private readonly string _name;
        public BasicItem(string name) => _name = name;
        public override Identifier Id => Identifier.WithDefaultNamespace(_name);
    }

    //RemainderItem 用掉后留下另一件物品的物品 岩浆桶烧完剩空桶走它
    private sealed class RemainderItem(string name, Func<Item> remainder) : Item
    {
        public override Identifier Id => Identifier.WithDefaultNamespace(name);
        public override Item? CraftingRemainder => remainder();
    }

    //AIR 空气物品 ITEM 注册表默认值 id 0
    public static readonly Item AIR = new BasicItem("air");

    //STONE 石头方块物品 id 1 放置时落位石头方块
    public static readonly Item STONE = new BlockItem("stone", Blocks.STONE);

    //GRASS_BLOCK 草方块物品 id 54
    public static readonly Item GRASS_BLOCK = new BlockItem("grass_block", Blocks.GRASS_BLOCK);

    //DIRT 泥土物品 id 55
    public static readonly Item DIRT = new BlockItem("dirt", Blocks.DIRT);

    //REDSTONE_TORCH 红石火把物品 点到侧面时落墙火把那一个变体
    public static readonly Item REDSTONE_TORCH = new BlockItem("redstone_torch", Blocks.REDSTONE_TORCH)
    {
        WallBlock = Blocks.REDSTONE_WALL_TORCH,
    };

    //REPEATER 红石中继器物品
    public static readonly Item REPEATER = new BlockItem("repeater", Blocks.REPEATER);

    //COMPARATOR 红石比较器物品
    public static readonly Item COMPARATOR = new BlockItem("comparator", Blocks.COMPARATOR);

    //REDSTONE 红石粉物品 原版红石线方块就是用它注册的 放置后落成红石线
    public static readonly Item REDSTONE = new BlockItem("redstone", Blocks.REDSTONE_WIRE);

    //OBSERVER 观察者物品
    public static readonly Item OBSERVER = new BlockItem("observer", Blocks.OBSERVER);

    //BUCKET 空桶
    public static readonly Item BUCKET = new BasicItem("bucket");

    //LAVA_BUCKET 岩浆桶 当燃料烧掉后留下空桶
    public static readonly Item LAVA_BUCKET = new RemainderItem("lava_bucket", () => BUCKET);

    //投射物类物品 发射器按 ProjectileItem 造实体射出去 玩家投掷将来也走同一条路
    public static readonly Item ARROW = new ArrowItem("arrow");

    public static readonly Item SNOWBALL = new SnowballItem("snowball");

    public static readonly Item EGG = new EggItem("egg");

    public static readonly Item ENDER_PEARL = new EnderPearlItem("ender_pearl");

    public static readonly Item FIRE_CHARGE = new FireChargeItem("fire_charge");

    //FLINT_AND_STEEL 打火石 对着方块使用在点击面外侧放火 行为在 IUseOnBlockItem
    public static readonly Item FLINT_AND_STEEL = new FlintAndSteelItem("flint_and_steel");

    //Implemented 已接入具体行为的物品 未列出的按原版注册名建占位物品
    //必须声明在具体物品字段之后 静态字段按文本顺序初始化
    private static readonly Dictionary<string, Item> Implemented = new(StringComparer.Ordinal)
    {
        ["air"] = AIR,
        ["stone"] = STONE,
        ["grass_block"] = GRASS_BLOCK,
        ["dirt"] = DIRT,
        ["redstone_torch"] = REDSTONE_TORCH,
        ["repeater"] = REPEATER,
        ["comparator"] = COMPARATOR,
        ["redstone"] = REDSTONE,
        ["observer"] = OBSERVER,
        ["bucket"] = BUCKET,
        ["lava_bucket"] = LAVA_BUCKET,
        ["arrow"] = ARROW,
        ["snowball"] = SNOWBALL,
        ["egg"] = EGG,
        ["ender_pearl"] = ENDER_PEARL,
        ["fire_charge"] = FIRE_CHARGE,
        ["flint_and_steel"] = FLINT_AND_STEEL,
    };

    //ByBlock 方块到对应物品的反查 对应原版 Item.BY_BLOCK
    //破坏方块取默认掉落时按它找 注册方块物品时填充
    private static readonly Dictionary<Block, Item> ByBlock = new();

    //ItemForBlock 取方块的对应物品 没有对应物品返回 null
    public static Item? ItemForBlock(Block block) => ByBlock.GetValueOrDefault(block);

    //Bootstrap 按原版注册顺序填充 ITEM 注册表
    //由 GameBootstrap 在 BootStrap 冻结注册表之前调用
    //id 必须与客户端本地 vanilla ITEM 表逐项对齐 否则 ItemStack 序号错位显示成别的物品
    public static void Bootstrap()
    {
        foreach (var name in VanillaItems.Order)
            Register(Implemented.TryGetValue(name, out var item) ? item : CreateDefault(name));
    }

    //CreateDefault 未接具体行为的物品: 有同名已注册方块的建成方块物品 其余建普通占位物品
    //方块物品是放置的前提 服务端要按 PlacedBlock 落位 客户端按本地表解析同一个物品 id
    private static Item CreateDefault(string name)
    {
        var id = Identifier.WithDefaultNamespace(name);
        //BLOCK 是 DefaultedRegistry 未知 id 取值兜底成 air 必须先判存在
        if (!BuiltInRegistries.BLOCK.ContainsKey(id)) return new BasicItem(name);
        var block = BuiltInRegistries.BLOCK.GetValue(id);
        return block is null ? new BasicItem(name) : new BlockItem(name, block);
    }

    //Register 注册物品到 ITEM 注册表 方块物品顺带建立方块反查
    private static void Register(Item item)
    {
        Registry<Item>.Register(BuiltInRegistries.ITEM, item.Id, item);
        if (item is BlockItem blockItem) ByBlock[blockItem.PlacedBlock] = item;
    }
}
