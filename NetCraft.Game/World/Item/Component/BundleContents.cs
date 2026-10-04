using NetCraft.Codec;
using NetCraft.Game.Util;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Inventory.Tooltip;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//BundleContents 收纳袋内容 对应原版 net.minecraft.world.item.component.BundleContents
//总重上限为 1 按单件重量配比装填 同种可堆叠物合并进同一格
public sealed class BundleContents : TooltipComponent
{
    public const int NoSelectedItemIndex = -1;
    private static readonly Fraction BundleInBundleWeight = Fraction.GetFraction(1, 16);
    private static readonly DataResult<Fraction> BeehiveWeight = DataResult<Fraction>.Success(Fraction.One);

    public static readonly BundleContents Empty = new(new List<ItemStackTemplate>());

    //PersistentCodec 持久化编解码 对应原版 CODEC 内容就是模板列表
    public static readonly Codec<BundleContents> PersistentCodec = ItemStackTemplate.PersistentCodec.ListOf().ComapFlatMap(
        items => DataResult<BundleContents>.Success(new BundleContents(items)),
        contents => contents.Items);

    public static readonly StreamCodec<RegistryFriendlyByteBuf, BundleContents> StreamCodec = new BundleContentsStreamCodec();

    private readonly List<ItemStackTemplate> _items;
    private readonly int _selectedItem;
    private readonly Lazy<DataResult<Fraction>> _weight;

    public BundleContents(IReadOnlyList<ItemStackTemplate> items) : this(items, NoSelectedItemIndex)
    {
    }

    private BundleContents(IReadOnlyList<ItemStackTemplate> items, int selectedItem)
    {
        _items = new List<ItemStackTemplate>(items);
        _selectedItem = selectedItem;
        _weight = new Lazy<DataResult<Fraction>>(() => ComputeContentWeight(_items));
    }

    public IReadOnlyList<ItemStackTemplate> Items => _items;

    public int Size => _items.Count;

    public bool IsEmpty => _items.Count == 0;

    public int GetSelectedItemIndex() => _selectedItem;

    public ItemStackTemplate? GetSelectedItem() => _selectedItem == NoSelectedItemIndex ? null : _items[_selectedItem];

    //Weight 内容总重 首次访问算一次并缓存
    public DataResult<Fraction> Weight() => _weight.Value;

    //GetNumberOfItemsToShow 提示框最多显示几格 超过 12 格要留出最后一行的空位
    public int GetNumberOfItemsToShow()
    {
        var numberOfItemStacks = Size;
        var availableItemsToShow = numberOfItemStacks > 12 ? 11 : 12;
        var itemsOnNonFullRow = numberOfItemStacks % 4;
        var emptySpaceOnNonFullRow = itemsOnNonFullRow == 0 ? 0 : 4 - itemsOnNonFullRow;
        return Math.Min(numberOfItemStacks, availableItemsToShow - emptySpaceOnNonFullRow);
    }

    //ItemCopyStream 逐件物化出物品栈
    public IEnumerable<ItemStack> ItemCopyStream()
    {
        foreach (var item in _items) yield return item.Create();
    }

    //CanItemBeInBundle 空物品与不允许进容器的物品都塞不进来
    public static bool CanItemBeInBundle(ItemStack item) => !item.IsEmpty() && item.GetItem().CanFitInsideContainerItems;

    public override bool Equals(object? obj) => obj is BundleContents other && _items.SequenceEqual(other._items);

    public override int GetHashCode() => _items.Count;

    public override string ToString() => $"BundleContents[{string.Join(", ", _items)}]";

    //ComputeContentWeight 各件重量乘数量累加 溢出按原版给出总重超限
    private static DataResult<Fraction> ComputeContentWeight(IReadOnlyList<ItemStackTemplate> items)
    {
        try
        {
            var weight = Fraction.Zero;
            foreach (var item in items)
            {
                var itemWeight = GetWeight(item);
                if (!itemWeight.Result().IsPresent) return itemWeight;
                weight = weight.Add(itemWeight.GetOrThrow().MultiplyBy(Fraction.GetFraction(item.Count)));
            }
            return DataResult<Fraction>.Success(weight);
        }
        catch (ArithmeticException)
        {
            return DataResult<Fraction>.Error(() => "Excessive total bundle weight");
        }
    }

    //GetWeight 收纳袋按 1/16 计入 装有蜜蜂的按 1 计 其余按堆叠上限的倒数
    private static DataResult<Fraction> GetWeight(ItemInstance item)
    {
        if (item.Get(DataComponents.BUNDLE_CONTENTS) is BundleContents bundle)
        {
            return bundle.Weight().Map(nestedWeight => nestedWeight.Add(BundleInBundleWeight));
        }
        if (item.Get(DataComponents.BEES) is Bees bees && bees.Occupants.Count > 0) return BeehiveWeight;
        return DataResult<Fraction>.Success(Fraction.GetFraction(1, item.GetMaxStackSize()));
    }

    //Mutable 收纳袋的可变版本 负责实际装填与取出
    public sealed class Mutable
    {
        private readonly List<ItemStack> _items;
        private Fraction _weight;
        private int _selectedItem;

        public Mutable(BundleContents contents)
        {
            var currentWeight = contents.Weight();
            if (!currentWeight.Result().IsPresent)
            {
                _items = new List<ItemStack>();
                _weight = Fraction.Zero;
                _selectedItem = NoSelectedItemIndex;
                return;
            }
            _items = new List<ItemStack>(contents._items.Count);
            foreach (var template in contents._items) _items.Add(template.Create());
            _weight = currentWeight.GetOrThrow();
            _selectedItem = contents._selectedItem;
        }

        public Fraction Weight => _weight;

        public Mutable ClearItems()
        {
            _items.Clear();
            _weight = Fraction.Zero;
            _selectedItem = NoSelectedItemIndex;
            return this;
        }

        //TryInsert 尽量装入并返回实际装入数量 装不下返回 0
        public int TryInsert(ItemStack itemsToAdd)
        {
            if (!CanItemBeInBundle(itemsToAdd)) return 0;
            var maybeItemWeight = GetWeight(itemsToAdd);
            if (!maybeItemWeight.Result().IsPresent) return 0;
            var itemWeight = maybeItemWeight.GetOrThrow();
            var amountToAdd = Math.Min(itemsToAdd.GetCount(), GetMaxAmountToAdd(itemWeight));
            if (amountToAdd == 0) return 0;
            _weight = _weight.Add(itemWeight.MultiplyBy(Fraction.GetFraction(amountToAdd)));
            var stackIndex = FindStackIndex(itemsToAdd);
            if (stackIndex != -1)
            {
                var removedStack = _items[stackIndex];
                _items.RemoveAt(stackIndex);
                var mergedStack = removedStack.CopyWithCount(removedStack.GetCount() + amountToAdd);
                itemsToAdd.Shrink(amountToAdd);
                _items.Insert(0, mergedStack);
            }
            else
            {
                _items.Insert(0, itemsToAdd.Split(amountToAdd));
            }
            return amountToAdd;
        }

        //TryTransfer 从槽位搬进收纳袋 返回实际搬入数量
        public int TryTransfer(Slot slot, Player player)
        {
            var other = slot.GetItem();
            var itemWeight = GetWeight(other);
            if (!itemWeight.Result().IsPresent) return 0;
            var maxAmount = GetMaxAmountToAdd(itemWeight.GetOrThrow());
            return CanItemBeInBundle(other) ? TryInsert(slot.SafeTake(other.GetCount(), maxAmount, player)) : 0;
        }

        public void ToggleSelectedItem(int selectedItem) => _selectedItem = _selectedItem == selectedItem || IndexIsOutsideAllowedBounds(selectedItem) ? NoSelectedItemIndex : selectedItem;

        //RemoveOne 取出一件 优先取选中格
        public ItemStack? RemoveOne()
        {
            if (_items.Count == 0) return null;
            var removeIndex = IndexIsOutsideAllowedBounds(_selectedItem) ? 0 : _selectedItem;
            var stack = _items[removeIndex].Copy();
            _items.RemoveAt(removeIndex);
            _weight = _weight.Subtract(GetWeight(stack).GetOrThrow().MultiplyBy(Fraction.GetFraction(stack.GetCount())));
            ToggleSelectedItem(NoSelectedItemIndex);
            return stack;
        }

        public BundleContents ToImmutable()
        {
            var templates = new List<ItemStackTemplate>(_items.Count);
            foreach (var item in _items) templates.Add(ItemStackTemplate.FromNonEmptyStack(item));
            return new BundleContents(templates, _selectedItem);
        }

        private int FindStackIndex(ItemStack itemsToAdd)
        {
            if (!itemsToAdd.IsStackable()) return -1;
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsSameItemAndComponentsAs(itemsToAdd)) return i;
            }
            return -1;
        }

        //GetMaxAmountToAdd 按剩余容纳量换算还能装几件
        private int GetMaxAmountToAdd(Fraction itemWeight)
        {
            var remainingWeight = Fraction.One.Subtract(_weight);
            return Math.Max(remainingWeight.DivideBy(itemWeight).IntValue, 0);
        }

        private bool IndexIsOutsideAllowedBounds(int selectedItem) => selectedItem < 0 || selectedItem >= _items.Count;
    }
}

//BundleContentsStreamCodec 对应原版 STREAM_CODEC 先写数量再逐个写模板
internal sealed class BundleContentsStreamCodec : StreamCodec<RegistryFriendlyByteBuf, BundleContents>
{
    public BundleContents Decode(RegistryFriendlyByteBuf buf)
    {
        var count = buf.ReadVarInt();
        var items = new List<ItemStackTemplate>(count);
        for (var i = 0; i < count; i++) items.Add(ItemStackTemplate.StreamCodec.Decode(buf));
        return new BundleContents(items);
    }

    public void Encode(RegistryFriendlyByteBuf buf, BundleContents value)
    {
        buf.WriteVarInt(value.Items.Count);
        foreach (var item in value.Items) ItemStackTemplate.StreamCodec.Encode(buf, item);
    }
}
