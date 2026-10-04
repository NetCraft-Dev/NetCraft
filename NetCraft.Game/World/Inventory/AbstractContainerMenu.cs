using NetCraft.Game.Server;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Logging;
using NetCraft.Network.Inventory;
using NetCraft.Util;

namespace NetCraft.Game.World.Inventory;

//AbstractContainerMenu 容器菜单对应原版 net.minecraft.world.inventory.AbstractContainerMenu
//持有槽位列表与状态号 负责处理上行点击并把结果同步给客户端
//最小实现覆盖拾取/快速移动/交换/丢弃/创造复制/双击收集 拖拽与容器数据槽暂缺
public abstract class AbstractContainerMenu
{
    //SlotClickedOutside 点击菜单外 用于丢出光标物品
    public const int SlotClickedOutside = -999;

    //CarriedSlotIndex 光标物品的伪槽位号 对应原版同步光标时用的 -1
    public const int CarriedSlotIndex = -1;

    private readonly List<Slot> _slots = new();
    private readonly List<ItemStack> _remoteSlots = new();
    //_dataSlots 数值型数据槽 熔炼进度与切石机选中项这类非物品状态走它同步
    //一个 ContainerData 可以有多个值(熔炉四格进度) 所以按 (容器,下标) 记而不是整只容器
    private readonly List<DataSlotRef> _dataSlots = new();
    //_remoteDataSlots 客户端已知的数据槽值 比对出变化才发包
    private readonly List<int> _remoteDataSlots = new();

    //DataSlotRef 数据槽引用 指向某个 ContainerData 的某个下标
    private readonly record struct DataSlotRef(ContainerData Data, int Index);
    private int _stateId;

    protected AbstractContainerMenu(int containerId) : this(null, containerId) { }

    //Kind 菜单类型 下发 open_screen 靠它告诉客户端开哪种界面 玩家背包菜单没有对应界面为 null
    protected AbstractContainerMenu(MenuType? kind, int containerId)
    {
        Kind = kind;
        ContainerId = containerId;
    }

    //ContainerId 菜单 id 客户端按它找到对应菜单
    public int ContainerId { get; }

    //Kind 菜单类型 对应原版 AbstractContainerMenu.getType 背包菜单为 null
    public MenuType? Kind { get; }

    //Slots 槽位列表 顺序即客户端看到的槽位号
    public IReadOnlyList<Slot> Slots => _slots;

    //StateId 状态号 每次同步自增 客户端据此丢弃过期包
    public int StateId => _stateId;

    //Carried 服务端权威的光标物品
    public ItemStack Carried { get; private set; } = ItemStack.Empty;

    //RemoteCarried 客户端当前认为的光标物品
    public ItemStack RemoteCarried { get; private set; } = ItemStack.Empty;

    //OwnerInventory 菜单所属玩家的物品栏 交换/丢出/收集需要 无玩家的菜单为 null
    protected PlayerInventory? OwnerInventory { get; init; }

    //Synchronizer 变更下发通道 未注入时只改本地状态
    public ContainerSynchronizer? Synchronizer { get; set; }

    //QuickMoveStack 快速移动 把指定槽物品搬到菜单定义的目标区间 返回搬运前的栈
    public abstract ItemStack QuickMoveStack(ServerPlayer player, int slotIndex);

    //StillValid 菜单对玩家是否仍然有效
    public abstract bool StillValid(ServerPlayer player);

    //AddSlot 登记槽位并分配菜单内下标
    protected Slot AddSlot(Slot slot)
    {
        slot.Index = _slots.Count;
        _slots.Add(slot);
        _remoteSlots.Add(ItemStack.Empty);
        return slot;
    }

    //GetSlot 取菜单槽位
    public Slot GetSlot(int index) => _slots[index];

    //AddDataSlots 登记一只数据容器的全部值 对应原版 addDataSlots
    protected void AddDataSlots(ContainerData data)
    {
        for (var i = 0; i < data.Count; i++)
        {
            _dataSlots.Add(new DataSlotRef(data, i));
            //初值当作客户端已经知道 避免刚打开菜单就先发一轮冗余数据
            _remoteDataSlots.Add(data.Get(i));
        }
    }

    //AddDataSlot 登记数值型数据槽 返回它首个值的槽位下标
    protected int AddDataSlot(ContainerData data)
    {
        var index = _dataSlots.Count;
        AddDataSlots(data);
        return index;
    }

    //GetDataValue 读数据槽当前值
    public int GetDataValue(int index)
    {
        var slot = _dataSlots[index];
        return slot.Data.Get(slot.Index);
    }

    //SetDataValue 写数据槽当前值 客户端同步过来的值走这里
    public void SetDataValue(int index, int value)
    {
        var slot = _dataSlots[index];
        slot.Data.Set(slot.Index, value);
    }

    //ClickMenuButton 客户端按钮点击 切石机选配方这类非槽位交互走它 返回是否被处理
    //对应原版 AbstractContainerMenu.clickMenuButton
    public virtual bool ClickMenuButton(ServerPlayer player, int buttonId) => false;

    //IsValidSlotIndex 下标是否落在槽位范围内
    public bool IsValidSlotIndex(int index) => (uint)index < _slots.Count;

    //FindSlot 按容器与容器内下标找菜单槽位 找不到返回 null
    protected Slot? FindSlot(Container container, int containerSlotIndex)
    {
        foreach (var slot in _slots)
            if (slot.SlotIndex == containerSlotIndex && ReferenceEquals(slot.Container, container))
                return slot;
        return null;
    }

    //SetCarried 设置服务端光标物品
    public void SetCarried(ItemStack stack) => Carried = stack ?? ItemStack.Empty;

    //SetRemoteCarried 记录客户端上报的光标物品
    public void SetRemoteCarried(ItemStack stack) => RemoteCarried = stack ?? ItemStack.Empty;

    //IncrementStateId 状态号自增
    public void IncrementStateId() => _stateId++;

    //InitializeContents 客户端侧初始化内容 对应原版 initializeContents
    public void InitializeContents(int stateId, IReadOnlyList<ItemStack> items, ItemStack carried)
    {
        for (var i = 0; i < _slots.Count && i < items.Count; i++)
            _slots[i].Set(items[i]);
        SetCarried(carried);
        _stateId = stateId;
    }

    //SendAllDataToRemote 全量下发当前内容(玩家进世界时的初始同步)
    //remote 侧必须存副本而不是引用: 玩家背包加物品走的是就地 SetCount 复用同一个栈对象
    //存引用会让脏槽比较恒等 表现为"物品捡到了但客户端数量不更新"
    public void SendAllDataToRemote()
    {
        for (var i = 0; i < _slots.Count; i++) _remoteSlots[i] = _slots[i].GetItem().Copy();
        RemoteCarried = Carried.Copy();
        Synchronizer?.SendContentUpdate(this, _remoteSlots, Carried);
    }

    //BroadcastChanges 只同步发生变化的槽与光标 点击后与每 tick 调用
    //remote 侧存副本 与 SendAllDataToRemote 同理 否则就地改数量的槽比较恒等不会下发
    public void BroadcastChanges()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var current = _slots[i].GetItem();
            if (SameStack(current, _remoteSlots[i])) continue;
            _remoteSlots[i] = current.Copy();
            Synchronizer?.SendSlotChange(this, i, current);
        }
        if (!SameStack(Carried, RemoteCarried))
        {
            RemoteCarried = Carried.Copy();
            Synchronizer?.SendSlotChange(this, CarriedSlotIndex, Carried);
        }
        //数据槽同样只在变化时下发 对应原版 broadcastChanges 里的 dataSlots 那一轮
        for (var i = 0; i < _dataSlots.Count; i++)
        {
            var value = GetDataValue(i);
            if (value == _remoteDataSlots[i]) continue;
            _remoteDataSlots[i] = value;
            Synchronizer?.SendDataChange(this, i, value);
        }
        IncrementStateId();
    }

    //Clicked 处理上行点击 对应原版 AbstractContainerMenu.clicked
    public void Clicked(int slotIndex, int buttonNum, ContainerInput input, ServerPlayer player)
    {
        if (!IsValidSlotIndex(slotIndex) && slotIndex != SlotClickedOutside)
        {
            //槽号越界多半是客户端与服务端槽位布局不一致 静默丢弃会让界面里按 Q 看起来毫无反应
            Log.Info($"[Container] Slot index out of range, dropped slot={slotIndex} menu slots={_slots.Count} input={input}");
            return;
        }
        var slot = IsValidSlotIndex(slotIndex) ? _slots[slotIndex] : null;
        switch (input)
        {
            case ContainerInput.Pickup:
                HandlePickup(slot, buttonNum);
                break;
            case ContainerInput.QuickMove:
                if (slot is not null) QuickMoveStack(player, slot.Index);
                break;
            case ContainerInput.Swap:
                HandleSwap(slot, buttonNum);
                break;
            case ContainerInput.Clone:
                HandleClone(slot, player);
                break;
            case ContainerInput.Throw:
                HandleThrow(slot, buttonNum, player);
                break;
            case ContainerInput.PickupAll:
                HandlePickupAll(slot);
                break;
            default:
                Log.Debug($"Container click type not implemented yet input={input} slot={slotIndex}");
                break;
        }
        BroadcastChanges();
    }

    //HandlePickup 左键拾取/放下 右键取一半/放一个 点菜单外丢出
    private void HandlePickup(Slot? slot, int buttonNum)
    {
        if (buttonNum != 0 && buttonNum != 1) return;
        if (slot is null)
        {
            if (Carried.IsEmpty()) return;
            var dropCount = buttonNum == 0 ? Carried.GetCount() : 1;
            SetCarried(Keep(Carried, Carried.GetCount() - dropCount));
            return;
        }
        var slotStack = slot.GetItem();
        var rightClick = buttonNum == 1;
        if (slotStack.IsEmpty())
        {
            if (Carried.IsEmpty() || !slot.MayPlace(Carried)) return;
            var placeCount = rightClick ? 1 : Math.Min(Carried.GetCount(), slot.GetMaxStackSize());
            slot.Set(Carried.CopyWithCount(placeCount));
            SetCarried(Keep(Carried, Carried.GetCount() - placeCount));
            return;
        }
        if (Carried.IsEmpty())
        {
            //空手拿起 右键只拿一半
            var takeCount = rightClick ? (slotStack.GetCount() + 1) / 2 : slotStack.GetCount();
            SetCarried(slot.Remove(takeCount));
            slot.SetChanged();
            return;
        }
        if (!slot.MayPlace(Carried)) return;
        //同种判定统一走 ItemStack 一处实现
        if (Carried.IsSameItemAndComponentsAs(slotStack))
        {
            var max = slot.GetMaxStackSize();
            if (slotStack.GetCount() >= max) return;
            var moveCount = rightClick ? 1 : Math.Min(max - slotStack.GetCount(), Carried.GetCount());
            slot.Set(slotStack.CopyWithCount(slotStack.GetCount() + moveCount));
            SetCarried(Keep(Carried, Carried.GetCount() - moveCount));
            return;
        }
        //不同物品 左键交换(右键不同物品不动作 对应原版)
        if (rightClick) return;
        slot.Set(Carried);
        SetCarried(slotStack);
    }

    //HandleSwap 数字键或 F 与快捷栏/副手槽交换 目标槽是点击槽本身时不动作
    private void HandleSwap(Slot? slot, int buttonNum)
    {
        if (slot is null || OwnerInventory is null) return;
        if (buttonNum is < 0 or > PlayerInventory.HotbarSlots) return;
        var target = FindSlot(OwnerInventory, buttonNum);
        if (target is null || ReferenceEquals(target, slot)) return;
        var temp = slot.GetItem();
        slot.Set(target.GetItem());
        target.Set(temp);
    }

    //HandleClone 创造模式中键复制 光标为空则取整叠 光标有物品则补满
    private void HandleClone(Slot? slot, ServerPlayer player)
    {
        if (slot is null || player.GameType != GameType.Creative) return;
        var slotStack = slot.GetItem();
        if (Carried.IsEmpty())
        {
            if (slotStack.IsEmpty()) return;
            SetCarried(slotStack.CopyWithCount(slotStack.GetItem().GetDefaultMaxStackSize()));
            return;
        }
        SetCarried(Carried.CopyWithCount(Carried.GetItem().GetDefaultMaxStackSize()));
    }

    //HandleThrow 丢弃 左键丢一个右键丢整叠 对应原版 AbstractContainerMenu 的 THROW 分支
    //光标上有物品时不处理 原版这条分支要求 getCarried().isEmpty()
    //槽位取出的物品走玩家丢弃路径落成掉落物 只从槽位移除不掉物的话容器界面里按 Q 等于白按
    private void HandleThrow(Slot? slot, int buttonNum, ServerPlayer player)
    {
        if (slot is null || !slot.HasItem() || !Carried.IsEmpty())
        {
            //门槛不满足时打日志 客户端按 Q 服务端却没动作时能直接看出卡在哪一条
            Log.Info($"[Container] Throw skipped slot={(slot is null ? "none" : slot.Index.ToString())} has item={slot?.HasItem()} carried not empty={!Carried.IsEmpty()}");
            return;
        }
        var count = buttonNum == 0 ? 1 : slot.GetItem().GetCount();
        var dropped = slot.Remove(count);
        slot.SetChanged();
        var entity = player.Drop(dropped, randomly: false, thrownFromHand: true);
        Log.Info($"[Container] Dropped slot={slot.Index} count={dropped.GetCount()} entity={(entity is null ? "not spawned" : entity.EntityId.ToString())}");
    }

    //HandlePickupAll 双击收集 把物品栏内与点击槽同种物品并入该槽
    private void HandlePickupAll(Slot? slot)
    {
        if (slot is null || OwnerInventory is null) return;
        var target = slot.GetItem();
        if (target.IsEmpty()) return;
        for (var i = 0; i < PlayerInventory.BackpackSize; i++)
        {
            var current = slot.GetItem();
            var max = slot.GetMaxStackSize();
            if (current.GetCount() >= max) break;
            var source = FindSlot(OwnerInventory, i);
            if (source is null || ReferenceEquals(source, slot) || !source.HasItem()) continue;
            //同种判定统一走 ItemStack 一处实现 别再各自写一份只比物品的版本
            if (!current.IsSameItemAndComponentsAs(source.GetItem())) continue;
            var moveCount = Math.Min(max - current.GetCount(), source.GetItem().GetCount());
            slot.Set(current.CopyWithCount(current.GetCount() + moveCount));
            source.Remove(moveCount);
            source.SetChanged();
        }
    }

    //MoveItemStackTo 在槽位区间内合并或放置指定栈 返回是否发生搬运
    //先与同类槽合并再找空槽 与原版顺序一致 reverse 表示从区间尾部往前找
    protected bool MoveItemStackTo(ItemStack stack, int startIndex, int endIndex, bool reverse)
    {
        var changed = false;
        var index = reverse ? endIndex - 1 : startIndex;
        var step = reverse ? -1 : 1;
        while (index >= startIndex && index < endIndex)
        {
            var slot = _slots[index];
            var slotStack = slot.GetItem();
            //同种判定用内核的 isSameItemSameComponents 只比物品会把不同组件的堆错误合并
            if (!slotStack.IsEmpty() && stack.IsSameItemAndComponentsAs(slotStack))
            {
                var max = slot.GetMaxStackSize();
                var total = stack.GetCount() + slotStack.GetCount();
                if (total <= max)
                {
                    slot.Set(slotStack.CopyWithCount(total));
                    stack.SetCount(0);
                    slot.SetChanged();
                    changed = true;
                }
                else if (slotStack.GetCount() < max)
                {
                    var moveCount = max - slotStack.GetCount();
                    slot.Set(slotStack.CopyWithCount(max));
                    stack.SetCount(stack.GetCount() - moveCount);
                    slot.SetChanged();
                    changed = true;
                }
            }
            index += step;
        }
        if (!stack.IsEmpty())
        {
            index = reverse ? endIndex - 1 : startIndex;
            while (index >= startIndex && index < endIndex)
            {
                var slot = _slots[index];
                if (slot.GetItem().IsEmpty() && slot.MayPlace(stack))
                {
                    var max = slot.GetMaxStackSize();
                    var placeCount = Math.Min(stack.GetCount(), max);
                    slot.Set(stack.CopyWithCount(placeCount));
                    stack.SetCount(stack.GetCount() - placeCount);
                    slot.SetChanged();
                    changed = true;
                    break;
                }
                index += step;
            }
        }
        return changed;
    }

    //Keep 从栈中保留 remain 个 返回新栈或空栈
    private static ItemStack Keep(ItemStack stack, int remain)
        => remain <= 0 ? ItemStack.Empty : stack.CopyWithCount(remain);

    //SameStack 栈内容是否完全一致 用于脏槽判定
    //同种判定走内核的同物品同组件比较 这里只额外比数量
    //引用短路只会命中空栈单例(ItemStack.Copy 对空栈返回 Empty) 非空栈两边一定是不同对象
    private static bool SameStack(ItemStack a, ItemStack b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.IsEmpty() || b.IsEmpty()) return a.IsEmpty() && b.IsEmpty();
        return a.GetCount() == b.GetCount() && a.IsSameItemAndComponentsAs(b);
    }

    //MaxContainerStackSize 容器侧堆叠上限 与原版 Container.getMaxStackSize 的默认值一致
    private const int MaxContainerStackSize = 64;

    //GetRedstoneSignalFromBlockEntity 方块实体的比较器信号 不是容器返回 0 对应原版同名方法
    public static int GetRedstoneSignalFromBlockEntity(object? blockEntity)
        => blockEntity is Container container ? GetRedstoneSignalFromContainer(container) : 0;

    //GetRedstoneSignalFromContainer 按容器填充度算比较器输出 对应原版同名方法
    //全空返回 0 满格返回 15 中间按每格物品占该格堆叠上限的比例取平均
    public static int GetRedstoneSignalFromContainer(Container? container)
    {
        if (container is null || container.Size <= 0) return 0;
        var nonEmptySlots = 0;
        var fill = 0f;
        for (var i = 0; i < container.Size; i++)
        {
            var stack = container.GetItem(i);
            if (stack.IsEmpty()) continue;
            fill += (float)stack.GetCount() / Math.Min(MaxContainerStackSize, stack.GetMaxStackSize());
            nonEmptySlots++;
        }
        return Mth.Floor(fill / container.Size * 14f) + (nonEmptySlots > 0 ? 1 : 0);
    }
}
