using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Inventory;

namespace NetCraft.Game.World.Inventory;

//AbstractCraftingMenu base class for menus with a crafting grid, maps to vanilla net.minecraft.world.inventory.AbstractCraftingMenu
//The inventory 2x2 and the crafting table 3x3 share the result recompute and ingredient consumption logic, only the grid size and slot coordinates differ
public abstract class AbstractCraftingMenu : AbstractContainerMenu
{
    protected AbstractCraftingMenu(MenuType? kind, int containerId, int width, int height)
        : base(kind, containerId)
    {
        CraftWidth = width;
        CraftHeight = height;
        CraftSlots = new SimpleContainer(width * height);
        ResultSlots = new SimpleContainer(1);
        //Recomputes the result whenever the crafting grid changes, maps to vanilla AbstractCraftingMenu.slotsChanged
        CraftSlots.Changed += _ => UpdateCraftingResult();
    }

    //CraftWidth/CraftHeight crafting grid width and height, 2x2 for the inventory and 3x3 for the crafting table
    public int CraftWidth { get; }
    public int CraftHeight { get; }

    //CraftSlotCount total number of crafting slots
    public int CraftSlotCount => CraftWidth * CraftHeight;

    //CraftSlots crafting grid container, subclasses lay out slots at their own coordinates
    protected SimpleContainer CraftSlots { get; }

    //ResultSlots result container with a single slot
    protected SimpleContainer ResultSlots { get; }

    //BuildCraftingInput reads the crafting grid into recipe input, indices are row-major
    private CraftingInput BuildCraftingInput()
    {
        var items = new ItemStack[CraftSlotCount];
        for (var i = 0; i < items.Length; i++) items[i] = CraftSlots.GetItem(i);
        return CraftingInput.Of(CraftWidth, CraftHeight, items);
    }

    //UpdateCraftingResult recomputes the result slot, maps to vanilla slotChangedCraftingGrid
    //The result slot stays empty while the recipe manager is not yet wired up
    public void UpdateCraftingResult()
    {
        var recipes = RecipeManager.Active;
        ResultSlots.SetItem(0, recipes is null ? ItemStack.Empty : recipes.GetCraftingResult(BuildCraftingInput()));
    }

    //OnCraftingTaken consumes ingredients from the crafting grid and recomputes after the result is taken, maps to vanilla ResultSlot.onTake
    //Decrements each non-empty crafting slot; triggering Changed afterwards recomputes automatically
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
