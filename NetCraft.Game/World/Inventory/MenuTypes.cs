using NetCraft.Network.Inventory;
using NetCraft.Registry;

namespace NetCraft.Game.World.Inventory;

//MenuTypes 内置菜单类型注册对应原版 net.minecraft.world.inventory.MenuType 的静态字段
//MENU 注册表 id 必须与原版一致 客户端按 id 反查界面 自行编号会让打开的界面类型对不上
//本作真正用得上的是箱式 generic_9x1..9x6 与工作台 crafting
//中间几个原版有而本作未接的类型按原版声明序占位注册 不占位的话 crafting 的 id 会整体前移
public static class MenuTypes
{
    public static readonly MenuType GENERIC_9X1 = new();
    public static readonly MenuType GENERIC_9X2 = new();
    public static readonly MenuType GENERIC_9X3 = new();
    public static readonly MenuType GENERIC_9X4 = new();
    public static readonly MenuType GENERIC_9X5 = new();
    public static readonly MenuType GENERIC_9X6 = new();

    //原版 generic_3x3(6) crafter_3x3(7) anvil(8) beacon(9) blast_furnace(10) brewing_stand(11) 本作未接
    public static readonly MenuType GENERIC_3X3 = new();
    public static readonly MenuType CRAFTER_3X3 = new();
    public static readonly MenuType ANVIL = new();
    public static readonly MenuType BEACON = new();
    public static readonly MenuType BLAST_FURNACE = new();
    public static readonly MenuType BREWING_STAND = new();

    //crafting(12) 工作台
    public static readonly MenuType CRAFTING = new();

    //原版 enchantment(13) furnace(14) grindstone(15) hopper(16) lectern(17) loom(18)
    //merchant(19) shulker_box(20) smithing(21) smoker(22) cartography_table(23) 本作未接
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

    //stonecutter(24) 切石机
    public static readonly MenuType STONECUTTER = new();

    //Bootstrap 注册内置菜单类型 必须在注册表冻结之前调
    //顺序即注册表 id 与原版 MenuType 静态字段声明序一一对应
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
