using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.Client.Inventory;

//ClientInventory 客户端物品栏缓存对应原版 LocalPlayer 的 Inventory 与当前打开的菜单槽位
//菜单槽位由 ClientboundContainerSetContent/SetSlot 填充 玩家物品栏由 ClientboundSetPlayerInventory 填充
public sealed class ClientInventory
{
    //PlayerSlotCount 玩家物品栏槽位数 布局与 PlayerInventory 一致
    public const int PlayerSlotCount = PlayerInventory.TotalSize;

    private readonly List<ItemStack> _menuSlots = new();
    private readonly ItemStack[] _playerSlots = new ItemStack[PlayerSlotCount];

    public ClientInventory()
    {
        for (var i = 0; i < _playerSlots.Length; i++) _playerSlots[i] = ItemStack.Empty;
    }

    //ContainerId 当前菜单 id 未收到内容包时是 -1
    public int ContainerId { get; private set; } = -1;

    //StateId 服务端最近一次同步的状态号
    public int StateId { get; private set; }

    //Carried 光标物品
    public ItemStack Carried { get; private set; } = ItemStack.Empty;

    //MenuSlots 菜单槽位只读视图 顺序与服务端菜单槽位号一致
    public IReadOnlyList<ItemStack> MenuSlots => _menuSlots;

    //GetPlayerItem 取玩家物品栏槽位 越界返回空栈
    public ItemStack GetPlayerItem(int slot)
        => (uint)slot < PlayerSlotCount ? _playerSlots[slot] : ItemStack.Empty;

    //SetContent 应用容器全量内容
    public void SetContent(int containerId, int stateId, IReadOnlyList<ItemStack> items, ItemStack carried)
    {
        ContainerId = containerId;
        StateId = stateId;
        _menuSlots.Clear();
        _menuSlots.AddRange(items);
        Carried = carried;
    }

    //SetSlot 应用单槽变更 槽号 -1 表示光标物品
    public void SetSlot(int slot, ItemStack stack)
    {
        if (slot == AbstractContainerMenu.CarriedSlotIndex)
        {
            Carried = stack;
            return;
        }
        if ((uint)slot >= _menuSlots.Count) return;
        _menuSlots[slot] = stack;
    }

    //SetPlayerSlot 应用玩家物品栏单槽变更
    public void SetPlayerSlot(int slot, ItemStack stack)
    {
        if ((uint)slot >= PlayerSlotCount) return;
        _playerSlots[slot] = stack;
    }
}
