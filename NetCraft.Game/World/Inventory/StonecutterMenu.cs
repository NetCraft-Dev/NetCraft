using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//StonecutterMenu stonecutter menu, maps to vanilla net.minecraft.world.inventory.StonecutterMenu
//Slot layout follows vanilla: 0 input, 1 result, 2-28 main inventory, 29-37 hotbar, 38 slots total
//The available recipe list is rebuilt when the input item changes, the client selects via button packets and the result slot produces the selection
public sealed class StonecutterMenu : AbstractContainerMenu
{
    public const int InputSlotIndex = 0;
    public const int ResultSlotIndex = 1;
    public const int InvSlotStart = 2;
    public const int InvSlotEnd = 29;
    public const int HotbarSlotStart = 29;
    public const int HotbarSlotEnd = 38;

    //SelectedRecipeDataId container data slot index of the selected recipe, the client reads it to highlight the current entry
    public const int SelectedRecipeDataId = 0;

    //_selectedRecipeIndex index of the selected recipe in the available list, -1 means nothing is selected
    private readonly DataSlot _selectedRecipeIndex = DataSlot.Standalone();
    private readonly SimpleContainer _input = new(1);
    private readonly SimpleContainer _result = new(1);
    private IReadOnlyList<StonecutterRecipe> _recipes = Array.Empty<StonecutterRecipe>();
    //_lastInput input used to build the last list, count changes do not rebuild it, matching the is(item) check in vanilla slotsChanged
    private ItemStack _lastInput = ItemStack.Empty;

    public StonecutterMenu(int containerId, PlayerInventory inventory)
        : base(MenuTypes.STONECUTTER, containerId)
    {
        OwnerInventory = inventory;
        AddSlot(new Slot(_input, 0, 20, 33));
        AddSlot(new StonecutterResultSlot(this, _result, 0, 143, 33));
        //2-28 main inventory, 29-37 hotbar, maps to vanilla addStandardInventorySlots(inventory, 8, 84)
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, 142));
        AddDataSlot(_selectedRecipeIndex);
        //Rebuilds the recipe list when the input slot changes, maps to the input container overriding setChanged in the vanilla menu
        _input.Changed += _ => OnInputChanged();
    }

    //AvailableRecipeCount number of recipes available for the current input
    public int AvailableRecipeCount => _recipes.Count;

    //SelectedRecipeIndex index of the current recipe, -1 means none selected
    public int SelectedRecipeIndex => _selectedRecipeIndex.Get(0);

    //SelectedRecipe the currently selected recipe, null when unselected or out of range
    public StonecutterRecipe? SelectedRecipe
        => (uint)SelectedRecipeIndex < (uint)_recipes.Count ? _recipes[SelectedRecipeIndex] : null;

    public override bool StillValid(ServerPlayer player) => true;

    //ClickMenuButton client recipe selection, maps to vanilla clickMenuButton
    //Clicking the same entry again returns false, same as vanilla
    public override bool ClickMenuButton(ServerPlayer player, int buttonId)
    {
        if (SelectedRecipeIndex == buttonId) return false;
        if ((uint)buttonId < (uint)_recipes.Count)
        {
            _selectedRecipeIndex.Set(0, buttonId);
            UpdateResult();
        }
        return true;
    }

    //OnInputChanged rebuilds the list only when the input item changes, a pure count change keeps the selected recipe
    private void OnInputChanged()
    {
        var input = _input.GetItem(0);
        if (SameItem(_lastInput, input)) return;
        _lastInput = input.Copy();
        //When the list changes the old selection may no longer hold, so the selection is cleared first as in vanilla
        _selectedRecipeIndex.Set(0, -1);
        _recipes = RecipeManager.Active?.GetStonecutterRecipes(input) ?? Array.Empty<StonecutterRecipe>();
        UpdateResult();
    }

    //UpdateResult produces the selected recipe in the result slot
    private void UpdateResult()
    {
        var recipe = SelectedRecipe;
        _result.SetItem(0,
            recipe is null ? ItemStack.Empty : recipe.Assemble(new SingleRecipeInput(_input.GetItem(0))));
    }

    //ConsumeInput consumes one input after the result is taken, maps to vanilla ResultSlot.onTake
    //The list is kept when the item type does not change, only the result is recomputed
    internal void ConsumeInput()
    {
        _input.RemoveItem(0, 1);
        UpdateResult();
    }

    //QuickMoveStack quick move: the result slot goes to the inventory, valid inputs fill the input slot and the rest shifts between the two inventory ranges
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            //Input is only consumed when a move happened; an item that does not fit must not be charged
            if (MoveItemStackTo(stack, InvSlotStart, HotbarSlotEnd, true)) ConsumeInput();
        }
        else if (slotIndex == InputSlotIndex)
            MoveItemStackTo(stack, InvSlotStart, HotbarSlotEnd, false);
        else if (IsStonecutterInput(stack))
            MoveItemStackTo(stack, InputSlotIndex, InputSlotIndex + 1, false);
        else if (slotIndex < InvSlotEnd)
            MoveItemStackTo(stack, HotbarSlotStart, HotbarSlotEnd, false);
        else
            MoveItemStackTo(stack, InvSlotStart, InvSlotEnd, false);
        slot.SetChanged();
        return moved;
    }

    //IsStonecutterInput whether the stack is a valid stonecutter input, maps to vanilla stonecutterRecipes().acceptsInput
    private static bool IsStonecutterInput(ItemStack stack)
        => RecipeManager.Active?.GetStonecutterRecipes(stack).Count > 0;

    //SameItem compares only the item, not the count, maps to vanilla ItemStack.is(Item)
    private static bool SameItem(ItemStack first, ItemStack second)
    {
        if (first.IsEmpty() || second.IsEmpty()) return first.IsEmpty() && second.IsEmpty();
        return ReferenceEquals(first.GetItem(), second.GetItem());
    }

    //StonecutterResultSlot result slot, nothing can be placed into it and taking consumes one input
    private sealed class StonecutterResultSlot : Slot
    {
        private readonly StonecutterMenu _menu;

        public StonecutterResultSlot(StonecutterMenu menu, Container container, int slotIndex, int x, int y)
            : base(container, slotIndex, x, y) => _menu = menu;

        public override bool MayPlace(ItemStack stack) => false;

        public override ItemStack Remove(int count)
        {
            var taken = base.Remove(count);
            if (!taken.IsEmpty()) _menu.ConsumeInput();
            return taken;
        }
    }
}
