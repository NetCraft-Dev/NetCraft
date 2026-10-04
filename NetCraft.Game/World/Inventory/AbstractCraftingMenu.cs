using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;
using NetCraft.Network.Inventory;

namespace NetCraft.Game.World.Inventory;

//AbstractCraftingMenu 带合成格的菜单基类对应原版 net.minecraft.world.inventory.AbstractCraftingMenu
//背包的 2x2 与工作台的 3x3 共用结果重算与扣料逻辑 差别只有网格尺寸与槽位坐标
public abstract class AbstractCraftingMenu : AbstractContainerMenu
{
    protected AbstractCraftingMenu(MenuType? kind, int containerId, int width, int height)
        : base(kind, containerId)
    {
        CraftWidth = width;
        CraftHeight = height;
        CraftSlots = new SimpleContainer(width * height);
        ResultSlots = new SimpleContainer(1);
        //合成格一变就重算结果 对应原版 AbstractCraftingMenu.slotsChanged
        CraftSlots.Changed += _ => UpdateCraftingResult();
    }

    //CraftWidth/CraftHeight 合成网格宽高 背包 2x2 工作台 3x3
    public int CraftWidth { get; }
    public int CraftHeight { get; }

    //CraftSlotCount 合成格总数
    public int CraftSlotCount => CraftWidth * CraftHeight;

    //CraftSlots 合成格容器 子类按各自坐标铺槽位
    protected SimpleContainer CraftSlots { get; }

    //ResultSlots 结果容器 只有一个槽
    protected SimpleContainer ResultSlots { get; }

    //BuildCraftingInput 把合成格读成配方输入 下标按行优先排列
    private CraftingInput BuildCraftingInput()
    {
        var items = new ItemStack[CraftSlotCount];
        for (var i = 0; i < items.Length; i++) items[i] = CraftSlots.GetItem(i);
        return CraftingInput.Of(CraftWidth, CraftHeight, items);
    }

    //UpdateCraftingResult 重算结果槽 对应原版 slotChangedCraftingGrid
    //配方管理器还没装配时结果槽保持空
    public void UpdateCraftingResult()
    {
        var recipes = RecipeManager.Active;
        ResultSlots.SetItem(0, recipes is null ? ItemStack.Empty : recipes.GetCraftingResult(BuildCraftingInput()));
    }

    //OnCraftingTaken 取走成品后扣掉合成格材料并重算 对应原版 ResultSlot.onTake
    //每个非空合成格减一份 减完触发 Changed 自动重算
    public void OnCraftingTaken()
    {
        for (var i = 0; i < CraftSlots.Size; i++)
        {
            if (CraftSlots.GetItem(i).IsEmpty()) continue;
            CraftSlots.RemoveItem(i, 1);
        }
        UpdateCraftingResult();
    }
}
