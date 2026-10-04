using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//StonecutterMenu 切石机菜单对应原版 net.minecraft.world.inventory.StonecutterMenu
//槽位布局照原版: 0 输入 1 结果 2-28 主物品栏 29-37 快捷栏 共 38 槽
//输入物品换了就重建可用配方列表 客户端按按钮包点选 结果槽按选中项产出
public sealed class StonecutterMenu : AbstractContainerMenu
{
    public const int InputSlotIndex = 0;
    public const int ResultSlotIndex = 1;
    public const int InvSlotStart = 2;
    public const int InvSlotEnd = 29;
    public const int HotbarSlotStart = 29;
    public const int HotbarSlotEnd = 38;

    //SelectedRecipeDataId 选中配方下标的容器数据槽号 客户端读它高亮当前项
    public const int SelectedRecipeDataId = 0;

    //_selectedRecipeIndex 选中配方在可用列表里的下标 -1 表示还没选
    private readonly DataSlot _selectedRecipeIndex = DataSlot.Standalone();
    private readonly SimpleContainer _input = new(1);
    private readonly SimpleContainer _result = new(1);
    private IReadOnlyList<StonecutterRecipe> _recipes = Array.Empty<StonecutterRecipe>();
    //_lastInput 上次建列表时的输入 数量变化不重建 与原版 slotsChanged 里的 is(item) 判定一致
    private ItemStack _lastInput = ItemStack.Empty;

    public StonecutterMenu(int containerId, PlayerInventory inventory)
        : base(MenuTypes.STONECUTTER, containerId)
    {
        OwnerInventory = inventory;
        AddSlot(new Slot(_input, 0, 20, 33));
        AddSlot(new StonecutterResultSlot(this, _result, 0, 143, 33));
        //2-28 主物品栏 29-37 快捷栏 对应原版 addStandardInventorySlots(inventory, 8, 84)
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            AddSlot(new Slot(inventory, PlayerInventory.HotbarSlots + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            AddSlot(new Slot(inventory, i, 8 + i * 18, 142));
        AddDataSlot(_selectedRecipeIndex);
        //输入槽变了就重建配方列表 对应原版菜单里那个覆写了 setChanged 的输入容器
        _input.Changed += _ => OnInputChanged();
    }

    //AvailableRecipeCount 当前输入可用的配方数
    public int AvailableRecipeCount => _recipes.Count;

    //SelectedRecipeIndex 当前选中的配方下标 -1 表示未选
    public int SelectedRecipeIndex => _selectedRecipeIndex.Get(0);

    //SelectedRecipe 当前选中的配方 未选或越界时为 null
    public StonecutterRecipe? SelectedRecipe
        => (uint)SelectedRecipeIndex < (uint)_recipes.Count ? _recipes[SelectedRecipeIndex] : null;

    public override bool StillValid(ServerPlayer player) => true;

    //ClickMenuButton 客户端点选配方 对应原版 clickMenuButton
    //重复点同一项返回 false 与原版一致
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

    //OnInputChanged 输入物品换了才重建列表 只是数量变化要保持已选配方
    private void OnInputChanged()
    {
        var input = _input.GetItem(0);
        if (SameItem(_lastInput, input)) return;
        _lastInput = input.Copy();
        //列表换了原选中项不一定还成立 与原版一样先清掉选择
        _selectedRecipeIndex.Set(0, -1);
        _recipes = RecipeManager.Active?.GetStonecutterRecipes(input) ?? Array.Empty<StonecutterRecipe>();
        UpdateResult();
    }

    //UpdateResult 结果槽按选中项产出
    private void UpdateResult()
    {
        var recipe = SelectedRecipe;
        _result.SetItem(0,
            recipe is null ? ItemStack.Empty : recipe.Assemble(new SingleRecipeInput(_input.GetItem(0))));
    }

    //ConsumeInput 取走成品后消耗一个输入 对应原版 ResultSlot.onTake
    //物品类型没变时列表保持 只重算结果
    internal void ConsumeInput()
    {
        _input.RemoveItem(0, 1);
        UpdateResult();
    }

    //QuickMoveStack 快捷搬运 结果槽搬进背包 能当输入的塞输入槽 其余在背包两区之间挪
    public override ItemStack QuickMoveStack(ServerPlayer player, int slotIndex)
    {
        var slot = GetSlot(slotIndex);
        if (!slot.HasItem()) return ItemStack.Empty;
        var moved = slot.GetItem().Copy();
        var stack = slot.GetItem();
        if (slotIndex == ResultSlotIndex)
        {
            //搬走了才消耗输入 背包塞不下(搬不动)不该白扣
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

    //IsStonecutterInput 该栈能否当切石机输入 对应原版 stonecutterRecipes().acceptsInput
    private static bool IsStonecutterInput(ItemStack stack)
        => RecipeManager.Active?.GetStonecutterRecipes(stack).Count > 0;

    //SameItem 只比物品本身不比数量 对应原版 ItemStack.is(Item)
    private static bool SameItem(ItemStack first, ItemStack second)
    {
        if (first.IsEmpty() || second.IsEmpty()) return first.IsEmpty() && second.IsEmpty();
        return ReferenceEquals(first.GetItem(), second.GetItem());
    }

    //StonecutterResultSlot 结果槽 不能往里放东西 取走时消耗一个输入
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
