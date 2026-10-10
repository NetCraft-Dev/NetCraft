using NetCraft.Game.World.Inventory;
using NetCraft.Registry;

namespace NetCraft.Game.World.Inventory;

//MenuTypes built-in menu type registration, maps to the static fields of vanilla net.minecraft.world.inventory.MenuType
//MENU registry ids must match vanilla; the client looks up the screen by id, custom numbering makes the opened screen type mismatch
//The ones actually used here are the chest types generic_9x1..9x6 and crafting for the crafting table
//Types vanilla has but this project does not support are registered as placeholders in vanilla declaration order; without them the crafting id would shift
public static class MenuTypes
{
    public static readonly MenuType GENERIC_9X1 = new();
    public static readonly MenuType GENERIC_9X2 = new();
    public static readonly MenuType GENERIC_9X3 = new();
    public static readonly MenuType GENERIC_9X4 = new();
    public static readonly MenuType GENERIC_9X5 = new();
    public static readonly MenuType GENERIC_9X6 = new();

    //Vanilla generic_3x3(6) crafter_3x3(7) anvil(8) beacon(9) blast_furnace(10) brewing_stand(11), not supported here
    public static readonly MenuType GENERIC_3X3 = new();
    public static readonly MenuType CRAFTER_3X3 = new();
    public static readonly MenuType ANVIL = new();
    public static readonly MenuType BEACON = new();
    public static readonly MenuType BLAST_FURNACE = new();
    public static readonly MenuType BREWING_STAND = new();

    //crafting(12) crafting table
    public static readonly MenuType CRAFTING = new();

    //Vanilla enchantment(13) furnace(14) grindstone(15) hopper(16) lectern(17) loom(18)
    //merchant(19) shulker_box(20) smithing(21) smoker(22) cartography_table(23), not supported here
    public static readonly MenuType ENCHANTMENT = new();
    public static readonly MenuType FURNACE = new();
    public static readonly MenuType GRINDSTONE = new();
    public static readonly MenuType HOPPER = new();
    public static readonly MenuType LECTERN = new();
    public static readonly MenuType LOOM = new();
    public static readonly MenuType MERCHANT = new();
    public static readonly MenuType SHULKER_BOX = new();
    public static readonly MenuType SMITHING = new();
    public static readonly MenuType SMOKER = new();
    public static readonly MenuType CARTOGRAPHY_TABLE = new();

    //stonecutter(24) stonecutter
    public static readonly MenuType STONECUTTER = new();

    //Bootstrap registers the built-in menu types, must be called before the registry is frozen
    //The order is the registry id and corresponds one-to-one with the vanilla MenuType static field declaration order
    public static void Bootstrap()
    {
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_9x1", GENERIC_9X1);
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_9x2", GENERIC_9X2);
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_9x3", GENERIC_9X3);
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_9x4", GENERIC_9X4);
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_9x5", GENERIC_9X5);
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_9x6", GENERIC_9X6);
        Registry<object>.Register(BuiltInRegistries.MENU, "generic_3x3", GENERIC_3X3);
        Registry<object>.Register(BuiltInRegistries.MENU, "crafter_3x3", CRAFTER_3X3);
        Registry<object>.Register(BuiltInRegistries.MENU, "anvil", ANVIL);
        Registry<object>.Register(BuiltInRegistries.MENU, "beacon", BEACON);
        Registry<object>.Register(BuiltInRegistries.MENU, "blast_furnace", BLAST_FURNACE);
        Registry<object>.Register(BuiltInRegistries.MENU, "brewing_stand", BREWING_STAND);
        Registry<object>.Register(BuiltInRegistries.MENU, "crafting", CRAFTING);
        Registry<object>.Register(BuiltInRegistries.MENU, "enchantment", ENCHANTMENT);
        Registry<object>.Register(BuiltInRegistries.MENU, "furnace", FURNACE);
        Registry<object>.Register(BuiltInRegistries.MENU, "grindstone", GRINDSTONE);
        Registry<object>.Register(BuiltInRegistries.MENU, "hopper", HOPPER);
        Registry<object>.Register(BuiltInRegistries.MENU, "lectern", LECTERN);
        Registry<object>.Register(BuiltInRegistries.MENU, "loom", LOOM);
        Registry<object>.Register(BuiltInRegistries.MENU, "merchant", MERCHANT);
        Registry<object>.Register(BuiltInRegistries.MENU, "shulker_box", SHULKER_BOX);
        Registry<object>.Register(BuiltInRegistries.MENU, "smithing", SMITHING);
        Registry<object>.Register(BuiltInRegistries.MENU, "smoker", SMOKER);
        Registry<object>.Register(BuiltInRegistries.MENU, "cartography_table", CARTOGRAPHY_TABLE);
        Registry<object>.Register(BuiltInRegistries.MENU, "stonecutter", STONECUTTER);
    }
}
