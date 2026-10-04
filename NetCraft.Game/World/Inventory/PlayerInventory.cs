using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Inventory;

//PlayerInventory 玩家物品栏对应原版 net.minecraft.world.entity.player.Inventory
//槽位布局: 0-8 快捷栏 9-35 主物品栏 36-39 护甲(头盔->靴子) 40 副手 共 41 格
//本类同时作为菜单的后端容器 菜单槽位按 InventoryMenu 的常量索引映射到这里的下标
public sealed class PlayerInventory : Container
{
    //HotbarSlots 快捷栏槽位数
    public const int HotbarSlots = 9;

    //MainSlots 主物品栏槽位数
    public const int MainSlots = 27;

    //BackpackSize 快捷栏加主物品栏共 36 格
    public const int BackpackSize = HotbarSlots + MainSlots;

    //ArmorSlots 护甲槽位数
    public const int ArmorSlots = 4;

    //TotalSize 总槽位数 含护甲与副手
    public const int TotalSize = BackpackSize + ArmorSlots + 1;

    //OffhandSlot 副手在容器内的下标
    public const int OffhandSlot = BackpackSize + ArmorSlots;

    //_items 槽位内容 空槽统一用 ItemStack.Empty
    private readonly ItemStack[] _items = new ItemStack[TotalSize];
    private int _selected;

    public PlayerInventory()
    {
        for (var i = 0; i < _items.Length; i++) _items[i] = ItemStack.Empty;
    }

    public int Size => TotalSize;

    //SelectedSlot 当前选中的快捷栏槽位 0-8 越界按绕回处理
    public int SelectedSlot
    {
        get => _selected;
        set => _selected = ((value % HotbarSlots) + HotbarSlots) % HotbarSlots;
    }

    //GetSelectedItem 取当前选中快捷栏槽位的物品
    public ItemStack GetSelectedItem() => _items[_selected];

    public ItemStack GetItem(int slot)
        => (uint)slot < TotalSize ? _items[slot] : ItemStack.Empty;

    public void SetItem(int slot, ItemStack stack)
    {
        if ((uint)slot >= TotalSize) return;
        _items[slot] = stack ?? ItemStack.Empty;
        SetChanged();
    }

    public ItemStack RemoveItem(int slot, int count)
    {
        if ((uint)slot >= TotalSize || count <= 0) return ItemStack.Empty;
        var stack = _items[slot];
        if (stack.IsEmpty()) return ItemStack.Empty;
        var taken = stack.GetCount() <= count ? stack : stack.CopyWithCount(count);
        var remain = stack.GetCount() - taken.GetCount();
        _items[slot] = remain <= 0 ? ItemStack.Empty : stack.CopyWithCount(remain);
        SetChanged();
        return taken;
    }

    //RemoveFromSelected 取走当前选中槽的物品 对应原版 removeFromSelected
    //all 为真整槽取走 为假只取一个
    public ItemStack RemoveFromSelected(bool all)
    {
        var stack = GetSelectedItem();
        return RemoveItem(SelectedSlot, all ? stack.GetMaxStackSize() : 1);
    }

    public ItemStack RemoveItemNoUpdate(int slot)
    {
        if ((uint)slot >= TotalSize) return ItemStack.Empty;
        var stack = _items[slot];
        _items[slot] = ItemStack.Empty;
        return stack;
    }

    public bool IsEmpty()
    {
        foreach (var stack in _items)
            if (!stack.IsEmpty()) return false;
        return true;
    }

    public void ClearContent()
    {
        for (var i = 0; i < _items.Length; i++) _items[i] = ItemStack.Empty;
    }

    //CanPlaceItem 玩家物品栏接受任意物品
    public bool CanPlaceItem(int slot, ItemStack stack) => true;

    //InfiniteMaterials 创造模式的无限材料 对应原版 player.hasInfiniteMaterials
    //背包塞不下时创造模式直接吞掉物品不报满 由玩家在游戏模式变化时同步
    public bool InfiniteMaterials { get; set; }

    //HasRemainingSpaceForItem 该槽还能不能继续塞进新的同种物品 对应原版 hasRemainingSpaceForItem
    //四个条件缺一不可 槽非空且同种同组件且本身可堆叠且没到堆叠上限
    private static bool HasRemainingSpaceForItem(ItemStack slotStack, ItemStack newStack)
        => !slotStack.IsEmpty()
            && slotStack.IsSameItemAndComponentsAs(newStack)
            && slotStack.IsStackable()
            && slotStack.GetCount() < slotStack.GetMaxStackSize();

    //GetFreeSlot 从头找第一个空槽 找不到返回 -1 对应原版 getFreeSlot
    //扫的是全部 41 格 护甲与副手也算可放位置
    public int GetFreeSlot()
    {
        for (var i = 0; i < _items.Length; i++)
            if (_items[i].IsEmpty()) return i;
        return -1;
    }

    //FindSlotMatchingItem 全背包找同物品同组件的槽 找不到返回 -1 对应原版 findSlotMatchingItem
    //扫全部 41 格 护甲与副手里的也算命中
    public int FindSlotMatchingItem(ItemStack stack)
    {
        for (var i = 0; i < _items.Length; i++)
            if (!_items[i].IsEmpty() && _items[i].IsSameItemAndComponentsAs(stack)) return i;
        return -1;
    }

    //GetSuitableHotbarSlot 取一个适合放新物品的快捷栏槽 对应原版 getSuitableHotbarSlot
    //从当前选中槽起绕一圈找第一个空槽 原版全满时再找第一个未附魔的
    //本作暂无附魔组件 所有物品都算未附魔 第二轮必然在选中槽就命中 于是等价于有空格给空格 没空格才用当前选中槽
    public int GetSuitableHotbarSlot()
    {
        for (var offset = 0; offset < HotbarSlots; offset++)
        {
            var slot = (_selected + offset) % HotbarSlots;
            if (_items[slot].IsEmpty()) return slot;
        }
        return _selected;
    }

    //AddAndPickItem 把物品放到合适的快捷栏槽并选中它 对应原版 addAndPickItem
    //该槽原本有东西时先把它挪去任意空位 找不到空位才真的顶掉
    public void AddAndPickItem(ItemStack stack)
    {
        SelectedSlot = GetSuitableHotbarSlot();
        var current = _items[_selected];
        if (!current.IsEmpty())
        {
            var free = GetFreeSlot();
            if (free != -1) _items[free] = current;
        }
        _items[_selected] = stack;
        SetChanged();
    }

    //PickSlot 把背包某槽的物品与合适的快捷栏槽互换 对应原版 pickSlot
    public void PickSlot(int slot)
    {
        SelectedSlot = GetSuitableHotbarSlot();
        var swapped = _items[_selected];
        _items[_selected] = _items[slot];
        _items[slot] = swapped;
        SetChanged();
    }

    //GetSlotWithRemainingSpace 找还能叠进的槽 顺序照原版 当前选中槽 -> 副手 40 -> 全量从头
    public int GetSlotWithRemainingSpace(ItemStack stack)
    {
        if (HasRemainingSpaceForItem(_items[_selected], stack)) return _selected;
        if (HasRemainingSpaceForItem(_items[OffhandSlot], stack)) return OffhandSlot;
        for (var i = 0; i < _items.Length; i++)
            if (HasRemainingSpaceForItem(_items[i], stack)) return i;
        return -1;
    }

    //AddResource 往指定槽塞 返回没塞进去的剩余数量 对应原版 addResource(int, ItemStack)
    private int AddResource(int slot, ItemStack stack)
    {
        var count = stack.GetCount();
        var slotStack = _items[slot];
        //空槽先落一个数量 0 的同种栈 原版就是先占位再 grow
        if (slotStack.IsEmpty())
        {
            slotStack = stack.CopyWithCount(0);
            _items[slot] = slotStack;
        }
        var toAdd = Math.Min(count, slotStack.GetMaxStackSize() - slotStack.GetCount());
        if (toAdd == 0) return count;
        slotStack.SetCount(slotStack.GetCount() + toAdd);
        SetChanged();
        return count - toAdd;
    }

    //AddResource 自动挑槽 对应原版 addResource(ItemStack)
    private int AddResource(ItemStack stack)
    {
        var slot = GetSlotWithRemainingSpace(stack);
        if (slot == -1) slot = GetFreeSlot();
        return slot == -1 ? stack.GetCount() : AddResource(slot, stack);
    }

    //Add 按原版 Inventory.add 语义把物品收进背包 返回是否放进去了至少一个
    //放入的部分从传入栈里扣掉 剩余量读 stack.GetCount() 这是原版契约调用方靠它判断掉落
    //循环直到某一轮一个都放不进去为止 每一轮都能放满一整堆 所以多堆物品会依次占后面的空槽
    public bool Add(ItemStack stack) => Add(-1, stack);

    //Add 指定槽位版本对应原版 add(int, ItemStack) 传 -1 表示自动选槽
    //耐久物品整栈独占一个空槽的分支等耐久组件接入后再补 现在所有物品都走合并路径
    public bool Add(int slot, ItemStack stack)
    {
        if (stack.IsEmpty()) return false;
        int lastSize;
        do
        {
            lastSize = stack.GetCount();
            stack.SetCount(slot == -1 ? AddResource(stack) : AddResource(slot, stack));
            if (stack.IsEmpty()) break;
        } while (stack.GetCount() < lastSize);
        //整栈一个都没进去又赶上创造模式 按原版直接吞掉不算失败
        if (stack.GetCount() != lastSize || !InfiniteMaterials) return stack.GetCount() < lastSize;
        stack.SetCount(0);
        return true;
    }

    //PlaceItemBackInInventory 把物品放回背包 放不下的部分掉在玩家脚下 对应原版 placeItemBackInInventory
    //菜单关闭或结果槽退回时用 与原版差异是这里不做逐槽 set_slot 回包 由调用方统一同步
    public void PlaceItemBackInInventory(ItemStack stack, Func<ItemStack, bool> dropFallback)
    {
        while (!stack.IsEmpty())
        {
            var slot = GetSlotWithRemainingSpace(stack);
            if (slot == -1) slot = GetFreeSlot();
            if (slot == -1)
            {
                dropFallback(stack);
                return;
            }
            var space = stack.GetMaxStackSize() - GetItem(slot).GetCount();
            Add(slot, stack.CopyWithCount(Math.Min(space, stack.GetCount())));
            stack.Shrink(space);
        }
    }

    //SetChanged 内容变更钩子 菜单在 AddSlot 时注册监听后由菜单转发同步
    public void SetChanged() => Changed?.Invoke(this);

    //Changed 内容变更事件 由菜单订阅后触发同步
    public event Action<Container>? Changed;
}
