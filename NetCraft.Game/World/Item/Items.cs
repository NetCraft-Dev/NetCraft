using NetCraft.Game.World.Level.Block;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//Items built-in item constants, maps to vanilla net.minecraft.world.item.Items
//Registered into the BuiltInRegistries.ITEM registry
//Filled item by item in the vanilla registration order from VanillaItems.Order, so the ITEM registry ids equal the vanilla ids
//The client resolves ItemStack indices from its local vanilla ITEM table, a shifted index shows the wrong item
public static class Items
{
    //BasicItem plain item with only a registry name
    private sealed class BasicItem : Item
    {
        private readonly string _name;
        public BasicItem(string name) => _name = name;
        public override Identifier Id => Identifier.WithDefaultNamespace(_name);
    }

    //RemainderItem item that leaves another item behind when used; a lava bucket burning out into an empty bucket goes through it
    private sealed class RemainderItem(string name, Func<Item> remainder) : Item
    {
        public override Identifier Id => Identifier.WithDefaultNamespace(name);
        public override Item? CraftingRemainder => remainder();
    }

    //AIR air item, the ITEM registry default with id 0
    public static readonly Item AIR = new BasicItem("air");

    //STONE stone block item with id 1, places a stone block
    public static readonly Item STONE = new BlockItem("stone", Blocks.STONE);

    //GRASS_BLOCK grass block item with id 54
    public static readonly Item GRASS_BLOCK = new BlockItem("grass_block", Blocks.GRASS_BLOCK);

    //DIRT dirt item with id 55
    public static readonly Item DIRT = new BlockItem("dirt", Blocks.DIRT);

    //REDSTONE_TORCH redstone torch item, clicking a side places the wall torch variant
    public static readonly Item REDSTONE_TORCH = new BlockItem("redstone_torch", Blocks.REDSTONE_TORCH)
    {
        WallBlock = Blocks.REDSTONE_WALL_TORCH,
    };

    //REPEATER redstone repeater item
    public static readonly Item REPEATER = new BlockItem("repeater", Blocks.REPEATER);

    //COMPARATOR redstone comparator item
    public static readonly Item COMPARATOR = new BlockItem("comparator", Blocks.COMPARATOR);

    //REDSTONE redstone dust item, vanilla registers the redstone wire block with it, placing it drops redstone wire
    public static readonly Item REDSTONE = new BlockItem("redstone", Blocks.REDSTONE_WIRE);

    //OBSERVER observer item
    public static readonly Item OBSERVER = new BlockItem("observer", Blocks.OBSERVER);

    //BUCKET empty bucket
    public static readonly Item BUCKET = new BasicItem("bucket");

    //LAVA_BUCKET lava bucket, leaves an empty bucket when burned as fuel
    public static readonly Item LAVA_BUCKET = new RemainderItem("lava_bucket", () => BUCKET);

    //Projectile items: the dispenser spawns and shoots them via ProjectileItem, player throwing will use the same path
    public static readonly Item ARROW = new ArrowItem("arrow");

    public static readonly Item SNOWBALL = new SnowballItem("snowball");

    public static readonly Item EGG = new EggItem("egg");

    public static readonly Item ENDER_PEARL = new EnderPearlItem("ender_pearl");

    public static readonly Item FIRE_CHARGE = new FireChargeItem("fire_charge");

    //FLINT_AND_STEEL flint and steel, using it on a block lights a fire outside the clicked face, behavior lives in IUseOnBlockItem
    public static readonly Item FLINT_AND_STEEL = new FlintAndSteelItem("flint_and_steel");

    //Implemented items with actual behavior wired up, everything not listed becomes a placeholder from the vanilla registry name
    //Must be declared after the concrete item fields, static fields initialize in textual order
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

    //ByBlock reverse lookup from block to item, maps to vanilla Item.BY_BLOCK
    //Used to find the default drop when a block is broken, populated when block items are registered
    private static readonly Dictionary<Block, Item> ByBlock = new();

    //ItemForBlock returns the item for a block, null when there is none
    public static Item? ItemForBlock(Block block) => ByBlock.GetValueOrDefault(block);

    //Bootstrap populates the ITEM registry in vanilla registration order
    //Called by GameBootstrap before BootStrap freezes the registries
    //Ids must align one-to-one with the client's local vanilla ITEM table, otherwise ItemStack indices shift and show the wrong item
    public static void Bootstrap()
    {
        foreach (var name in VanillaItems.Order)
            Register(Implemented.TryGetValue(name, out var item) ? item : CreateDefault(name));
    }

    //CreateDefault for items without wired behavior: build a block item when a registered block with the same name exists, otherwise a plain placeholder
    //Block items are a prerequisite for placement, the server places PlacedBlock and the client resolves the same item id from its local table
    private static Item CreateDefault(string name)
    {
        var id = Identifier.WithDefaultNamespace(name);
        //BLOCK is a DefaultedRegistry, unknown ids fall back to air, so existence must be checked first
        if (!BuiltInRegistries.BLOCK.ContainsKey(id)) return new BasicItem(name);
        var block = BuiltInRegistries.BLOCK.GetValue(id);
        return block is null ? new BasicItem(name) : new BlockItem(name, block);
    }

    //Register registers an item into the ITEM registry, block items also populate the block reverse lookup
    private static void Register(Item item)
    {
        Registry<Item>.Register(BuiltInRegistries.ITEM, item.Id, item);
        if (item is BlockItem blockItem) ByBlock[blockItem.PlacedBlock] = item;
    }
}
