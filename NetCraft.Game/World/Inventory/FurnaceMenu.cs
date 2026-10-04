using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level.Block;
using NetCraft.Network.Inventory;
using NCItems = NetCraft.Game.World.Items.Items;

namespace NetCraft.Game.World.Inventory;

//FurnaceMenu 熔炼界面菜单对应原版 net.minecraft.world.inventory.AbstractFurnaceMenu
//三格(输入/燃料/结果)加四个数据槽 熔炉 高炉 烟熏炉只有菜单类型与配方类型不同
public sealed class FurnaceMenu : AbstractContainerMenu
{
    public const int IngredientSlotIndex = 0;
    public const int FuelSlotIndex = 1;
    public const int ResultSlotIndex = 2;
    public const int SlotCount = 3;

    //背包区与快捷栏区在菜单内的下标范围 与原版 quickMoveStack 用的区间一致
    public const int InvSlotStart = 3;
    private const int InvSlotEnd = 30;
    private const int UseRowSlotStart = 30;
    private const int UseRowSlotEnd = 39;

    //InventoryTop 玩家背包首行 y 坐标 与原版 addStandardInventorySlots(inv, 8, 84) 对齐
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

    //Furnace 菜单对应的熔炼方块实体
    public AbstractFurnaceBlockEntity Furnace => _furnace;

    //StillValid 方块还在且玩家没走远才有效 对应原版 AbstractFurnaceMenu.stillValid
    public override bool StillValid(ServerPlayer player) => _furnace.StillValid(player);

    //CanSmelt 该物品在本菜单对应的配方类型里有配方 对应原版 canSmelt
    public bool CanSmelt(ItemStack stack)
        => RecipeManager.Active?.GetCookingRecipe(_furnace.CookingRecipeType, stack) is not null;

    //IsFuel 该物品能当燃料 对应原版 isFuel
    public bool IsFuel(ItemStack stack) => FuelValues.Active?.IsFuel(stack) == true;

    //QuickMoveStack 结果与输入燃料与背包互搬 对应原版 quickMoveStack 的分支顺序
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

    //FuelSlot 燃料槽 只收燃料与桶 且桶一格只能放一个 对应原版 FurnaceFuelSlot
    private sealed class FuelSlot(Container container, int slotIndex, int x, int y)
        : Slot(container, slotIndex, x, y)
    {
        public override bool MayPlace(ItemStack stack) => IsBucket(stack) || base.MayPlace(stack);

        public override int GetMaxStackSize() => IsBucket(GetItem()) ? 1 : base.GetMaxStackSize();

        private static bool IsBucket(ItemStack stack)
            => !stack.IsEmpty() && ReferenceEquals(stack.GetItem(), NCItems.BUCKET);
    }
}
