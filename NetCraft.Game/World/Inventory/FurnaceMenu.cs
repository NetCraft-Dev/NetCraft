using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Game.World.Inventory;
using NCItems = NetCraft.Game.World.Items.Items;

namespace NetCraft.Game.World.Inventory;

//FurnaceMenu smelting menu, maps to vanilla net.minecraft.world.inventory.AbstractFurnaceMenu
//Three slots (input/fuel/result) plus four data slots; the furnace, blast furnace and smoker differ only in menu type and recipe type
public sealed class FurnaceMenu : AbstractContainerMenu
{
    public const int IngredientSlotIndex = 0;
    public const int FuelSlotIndex = 1;
    public const int ResultSlotIndex = 2;
    public const int SlotCount = 3;

    //Menu index ranges of the inventory and hotbar sections, same ranges vanilla quickMoveStack uses
    public const int InvSlotStart = 3;
    private const int InvSlotEnd = 30;
    private const int UseRowSlotStart = 30;
    private const int UseRowSlotEnd = 39;

    //InventoryTop y coordinate of the inventory's first row, aligned with vanilla addStandardInventorySlots(inv, 8, 84)
    private const int InventoryTop = 84;

    private readonly AbstractFurnaceBlockEntity _furnace;

    private FurnaceMenu(MenuType kind, int containerId, PlayerInventory inventory, AbstractFurnaceBlockEntity furnace)
        : base(kind, containerId)
    {
        OwnerInventory = inventory;
        _furnace = furnace;
        AddSlot(new Slot(furnace, IngredientSlotIndex, 56, 17));
        AddSlot(new FuelSlot(furnace, FuelSlotIndex, 56, 53));
        AddSlot(new Slot(furnace, ResultSlotIndex, 116, 35));
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i,
                8 + i % 9 * 18, InventoryTop + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, InventoryTop + 58));
        AddDataSlots(furnace);
    }

    public static FurnaceMenu ForFurnace(int containerId, PlayerInventory inventory, AbstractFurnaceBlockEntity furnace)
        => new(MenuTypes.FURNACE, containerId, inventory, furnace);

    public static FurnaceMenu ForBlastFurnace(int containerId, PlayerInventory inventory, AbstractFurnaceBlockEntity furnace)
        => new(MenuTypes.BLAST_FURNACE, containerId, inventory, furnace);

    public static FurnaceMenu ForSmoker(int containerId, PlayerInventory inventory, AbstractFurnaceBlockEntity furnace)
        => new(MenuTypes.SMOKER, containerId, inventory, furnace);

    //Furnace smelting block entity backing the menu
    public AbstractFurnaceBlockEntity Furnace => _furnace;

    //StillValid valid while the block remains and the player is close enough, maps to vanilla AbstractFurnaceMenu.stillValid
    public override bool StillValid(ServerPlayer player) => _furnace.StillValid(player);

    //CanSmelt whether the item has a recipe in this menu's recipe type, maps to vanilla canSmelt
    public bool CanSmelt(ItemStack stack)
        => RecipeManager.Active?.GetCookingRecipe(_furnace.CookingRecipeType, stack) is not null;

    //IsFuel whether the item can be used as fuel, maps to vanilla isFuel
    public bool IsFuel(ItemStack stack) => FuelValues.Active?.IsFuel(stack) == true;

    //QuickMoveStack moves the result and input/fuel to and from the inventory, matching the branch order of vanilla quickMoveStack
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            if (!MoveItemStackTo(stack, InvSlotStart, UseRowSlotEnd, true)) return ItemStack.Empty;
        }
        else if (slotIndex is IngredientSlotIndex or FuelSlotIndex)
        {
            if (!MoveItemStackTo(stack, InvSlotStart, UseRowSlotEnd, false)) return ItemStack.Empty;
        }
        else if (CanSmelt(stack))
        {
            if (!MoveItemStackTo(stack, IngredientSlotIndex, IngredientSlotIndex + 1, false)) return ItemStack.Empty;
        }
        else if (IsFuel(stack))
        {
            if (!MoveItemStackTo(stack, FuelSlotIndex, FuelSlotIndex + 1, false)) return ItemStack.Empty;
        }
        else if (slotIndex is >= InvSlotStart and < InvSlotEnd)
        {
            if (!MoveItemStackTo(stack, UseRowSlotStart, UseRowSlotEnd, false)) return ItemStack.Empty;
        }
        else if (slotIndex is >= UseRowSlotStart and < UseRowSlotEnd
            && !MoveItemStackTo(stack, InvSlotStart, InvSlotEnd, false))
        {
            return ItemStack.Empty;
        }
        if (stack.IsEmpty()) slot.Set(ItemStack.Empty);
        else slot.SetChanged();
        return moved;
    }

    //FuelSlot fuel slot, accepts only fuel and buckets, and only one bucket per slot, maps to vanilla FurnaceFuelSlot
    private sealed class FuelSlot(Container container, int slotIndex, int x, int y)
        : Slot(container, slotIndex, x, y)
    {
        public override bool MayPlace(ItemStack stack) => IsBucket(stack) || base.MayPlace(stack);

        public override int GetMaxStackSize() => IsBucket(GetItem()) ? 1 : base.GetMaxStackSize();

        private static bool IsBucket(ItemStack stack)
            => !stack.IsEmpty() && ReferenceEquals(stack.GetItem(), NCItems.BUCKET);
    }
}
