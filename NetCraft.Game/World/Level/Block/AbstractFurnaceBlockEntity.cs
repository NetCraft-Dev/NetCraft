using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Network.Component;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NCItems = NetCraft.Game.World.Items.Items;

namespace NetCraft.Game.World.Level.Block;

//AbstractFurnaceBlockEntity 熔炼方块实体基类对应原版 net.minecraft.world.level.block.entity.AbstractFurnaceBlockEntity
//三格容器(输入/燃料/结果)加四个数据槽(燃烧剩余/燃烧总时长/烹饪进度/烹饪总时长)
//熔炉 高炉 烟熏炉共用它 差别只有配方类型与菜单类型
public abstract class AbstractFurnaceBlockEntity : BlockEntity, Container, ContainerData, MenuProvider
{
    public const int SlotInput = 0;
    public const int SlotFuel = 1;
    public const int SlotResult = 2;
    public const int SlotCount = 3;

    public const int DataLitTime = 0;
    public const int DataLitDuration = 1;
    public const int DataCookingProgress = 2;
    public const int DataCookingTotalTime = 3;
    public const int DataCount = 4;

    //BurnCoolSpeed 熄火后烹饪进度每刻回退量 对应原版 BURN_COOL_SPEED
    private const int BurnCoolSpeed = 2;

    //DefaultCookTime 查不到配方时的默认烹饪时长 对应原版 getTotalCookTime 的兜底 200
    private const int DefaultCookTime = 200;

    //MaxStackSize 方块实体容器的堆叠上限 对应原版 AbstractContainerBlockEntity 的 64
    private const int MaxStackSize = 64;

    //ContainerDistanceSqr 菜单失效距离平方 原版 8 格
    private const double ContainerDistanceSqr = 64.0;

    private readonly ItemStack[] _items = new ItemStack[SlotCount];
    //_litTimeRemaining 剩余燃烧刻数 _litTotalTime 本次燃料的总刻数 两者一起决定火焰图标进度
    private int _litTimeRemaining;
    private int _litTotalTime;
    //_cookingTimer 当前烹饪进度 _cookingTotalTime 本炉配方所需刻数
    private int _cookingTimer;
    private int _cookingTotalTime;

    protected AbstractFurnaceBlockEntity(BlockEntityType type, BlockPos pos) : base(type, pos)
    {
        for (var i = 0; i < SlotCount; i++) _items[i] = ItemStack.Empty;
    }

    //CookingRecipeType 该熔炼方块使用的配方类型 熔炉 smelting 高炉 blasting 烟熏炉 smoking
    public abstract string CookingRecipeType { get; }

    //IsLit 是否正在燃烧 供诊断与测试
    public bool IsLit => _litTimeRemaining > 0;

    //CookingProgress 当前烹饪进度
    public int CookingProgress => _cookingTimer;

    public abstract Component DisplayName { get; }

    public abstract AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player);

    //StillValid 方块还在原位且玩家在 8 格内才有效 对应原版 Container.stillValidBlockEntity
    public bool StillValid(ServerPlayer player)
    {
        if (Level is not ServerLevel level) return false;
        if (level.GetBlockEntity<AbstractFurnaceBlockEntity>(Pos) is not { } current) return false;
        if (!ReferenceEquals(current, this)) return false;
        var center = new Vec3(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
        return player.Position.DistanceToSqr(center) <= ContainerDistanceSqr;
    }

    //Tick 每刻推进一炉 对应原版 AbstractFurnaceBlockEntity.serverTick
    public override void Tick()
    {
        if (Level is not ServerLevel level) return;
        var wasLit = _litTimeRemaining > 0;
        if (wasLit) _litTimeRemaining--;
        var isLit = _litTimeRemaining > 0;

        var fuel = _items[SlotFuel];
        var ingredient = _items[SlotInput];
        var hasIngredient = !ingredient.IsEmpty();
        var hasFuel = !fuel.IsEmpty();
        var changed = false;

        if (isLit || (hasFuel && hasIngredient))
        {
            //有料才谈得上烧 没料时原版只把烹饪进度清零 不重置配方进度总时长
            if (hasIngredient)
            {
                if (FindRecipe(ingredient) is { } recipe)
                {
                    var burnResult = recipe.Assemble(new SingleRecipeInput(ingredient));
                    if (!burnResult.IsEmpty() && CanBurn(burnResult))
                    {
                        if (!isLit)
                        {
                            var newLitTime = FuelValues.Active?.BurnDuration(fuel) ?? 0;
                            _litTimeRemaining = newLitTime;
                            _litTotalTime = newLitTime;
                            if (newLitTime > 0)
                            {
                                ConsumeFuel(fuel);
                                isLit = true;
                                changed = true;
                            }
                        }
                        if (isLit)
                        {
                            _cookingTimer++;
                            if (_cookingTimer == _cookingTotalTime)
                            {
                                _cookingTimer = 0;
                                _cookingTotalTime = recipe.CookingTime;
                                Burn(ingredient, burnResult);
                                changed = true;
                            }
                        }
                        else
                        {
                            _cookingTimer = 0;
                        }
                    }
                    else
                    {
                        _cookingTimer = 0;
                    }
                }
            }
            else
            {
                _cookingTimer = 0;
            }
        }
        else if (_cookingTimer > 0)
        {
            _cookingTimer = Mth.Clamp(_cookingTimer - BurnCoolSpeed, 0, _cookingTotalTime);
        }

        if (wasLit != isLit)
        {
            changed = true;
            if (level.GetBlockState(Pos) is { } state)
                level.SetBlock(Pos, state.SetValue(BlockStateProperties.Lit, isLit), 3);
        }
        if (changed) SetChanged();
    }

    //FindRecipe 按本方块类型与输入物品查配方 没有命中返回 null
    private AbstractCookingRecipe? FindRecipe(ItemStack ingredient)
        => RecipeManager.Active?.GetCookingRecipe(CookingRecipeType, ingredient);

    //ConsumeFuel 烧掉一个燃料 用尽时换成剩余物(岩浆桶换空桶) 对应原版 consumeFuel
    private void ConsumeFuel(ItemStack fuel)
    {
        var fuelItem = fuel.GetItem();
        fuel.Shrink(1);
        if (!fuel.IsEmpty()) return;
        var remainder = fuelItem.CraftingRemainder;
        _items[SlotFuel] = remainder is null
            ? ItemStack.Empty
            : new ItemStack(remainder.BuiltInRegistryHolder, 1, DataComponentPatch.Empty);
    }

    //CanBurn 结果槽能不能再放下一份产物 对应原版 canBurn
    private bool CanBurn(ItemStack burnResult)
    {
        var resultSlot = _items[SlotResult];
        if (resultSlot.IsEmpty()) return true;
        if (!resultSlot.IsSameItemAndComponentsAs(burnResult)) return false;
        var resultCount = resultSlot.GetCount() + burnResult.GetCount();
        var maxResultCount = Math.Min(MaxStackSize, burnResult.GetMaxStackSize());
        return resultCount <= maxResultCount;
    }

    //Burn 产出一份成品并扣掉一个输入 湿海绵配空桶会被烤成水桶 对应原版 burn
    private void Burn(ItemStack ingredient, ItemStack result)
    {
        var resultSlot = _items[SlotResult];
        if (resultSlot.IsEmpty()) _items[SlotResult] = result.Copy();
        else resultSlot.SetCount(resultSlot.GetCount() + result.GetCount());
        if (IsItem(ingredient, "wet_sponge") && !_items[SlotFuel].IsEmpty() && IsItem(_items[SlotFuel], "bucket"))
            _items[SlotFuel] = CreateStack("water_bucket");
        ingredient.Shrink(1);
    }

    //GetTotalCookTime 该输入的配方烹饪时长 查不到按 200 对应原版 getTotalCookTime
    private int GetTotalCookTime()
    {
        var recipe = FindRecipe(_items[SlotInput]);
        return recipe?.CookingTime ?? DefaultCookTime;
    }

    public int Size => SlotCount;

    public ItemStack GetItem(int slot) => (uint)slot < SlotCount ? _items[slot] : ItemStack.Empty;

    //SetItem 写槽位 输入槽换了别的物品要重算本炉烹饪时长并清零进度 对应原版 setItem
    public void SetItem(int slot, ItemStack stack)
    {
        if ((uint)slot >= SlotCount) return;
        var target = stack ?? ItemStack.Empty;
        var oldStack = _items[slot];
        var same = !target.IsEmpty() && target.IsSameItemAndComponentsAs(oldStack);
        _items[slot] = target;
        if (target.GetCount() > MaxStackSize) target.SetCount(MaxStackSize);
        if (slot != SlotInput || same) return;
        if (Level is not ServerLevel) return;
        _cookingTotalTime = GetTotalCookTime();
        _cookingTimer = 0;
        SetChanged();
    }

    public ItemStack RemoveItem(int slot, int count)
    {
        if ((uint)slot >= SlotCount || count <= 0) return ItemStack.Empty;
        var stack = _items[slot];
        if (stack.IsEmpty()) return ItemStack.Empty;
        var taken = stack.GetCount() <= count ? stack : stack.CopyWithCount(count);
        var remain = stack.GetCount() - taken.GetCount();
        _items[slot] = remain <= 0 ? ItemStack.Empty : stack.CopyWithCount(remain);
        SetChanged();
        return taken;
    }

    public ItemStack RemoveItemNoUpdate(int slot)
    {
        if ((uint)slot >= SlotCount) return ItemStack.Empty;
        var stack = _items[slot];
        _items[slot] = ItemStack.Empty;
        return stack;
    }

    //SetChanged 本作内容变更靠菜单每刻扫槽与定时全量刷盘 没有额外脏标记要做
    public void SetChanged() { }

    public bool IsEmpty()
    {
        foreach (var stack in _items)
            if (!stack.IsEmpty()) return false;
        return true;
    }

    public void ClearContent()
    {
        for (var i = 0; i < SlotCount; i++) _items[i] = ItemStack.Empty;
    }

    //CanPlaceItem 结果槽只出不进 燃料槽只收燃料或空桶 对应原版 canPlaceItem
    public bool CanPlaceItem(int slot, ItemStack stack)
    {
        if (slot == SlotResult) return false;
        if (slot != SlotFuel) return true;
        if (FuelValues.Active?.IsFuel(stack) == true) return true;
        return IsItem(stack, "bucket") && !IsItem(_items[SlotFuel], "bucket");
    }

    public int Count => DataCount;

    //Get 读数据槽 对应原版 ContainerData 的匿名实现
    public int Get(int index) => index switch
    {
        DataLitTime => _litTimeRemaining,
        DataLitDuration => _litTotalTime,
        DataCookingProgress => _cookingTimer,
        DataCookingTotalTime => _cookingTotalTime,
        _ => 0,
    };

    //Set 写数据槽 客户端同步过来的值会走这里 服务端权威逻辑不会被它改坏
    public void Set(int index, int value)
    {
        switch (index)
        {
            case DataLitTime: _litTimeRemaining = value; break;
            case DataLitDuration: _litTotalTime = value; break;
            case DataCookingProgress: _cookingTimer = value; break;
            case DataCookingTotalTime: _cookingTotalTime = value; break;
        }
    }

    //SaveAdditional 落盘字段名与原版一致 槽位按原版 ContainerHelper 的 Slot/item 结构写
    public override void SaveAdditional(CompoundTag tag)
    {
        base.SaveAdditional(tag);
        tag.PutShort("cooking_time_spent", (short)_cookingTimer);
        tag.PutShort("cooking_total_time", (short)_cookingTotalTime);
        tag.PutShort("lit_time_remaining", (short)_litTimeRemaining);
        tag.PutShort("lit_total_time", (short)_litTotalTime);
        var items = new ListTag();
        for (var i = 0; i < SlotCount; i++)
        {
            var stack = _items[i];
            if (stack.IsEmpty()) continue;
            var entry = new CompoundTag();
            entry.PutByte("Slot", (byte)i);
            ItemStack.WriteNbt(entry, "item", stack);
            items.Add(entry);
        }
        tag.Put("Items", items);
    }

    public override void LoadAdditional(CompoundTag tag)
    {
        base.LoadAdditional(tag);
        _cookingTimer = tag.GetShortValue("cooking_time_spent");
        _cookingTotalTime = tag.GetShortValue("cooking_total_time");
        _litTimeRemaining = tag.GetShortValue("lit_time_remaining");
        _litTotalTime = tag.GetShortValue("lit_total_time");
        for (var i = 0; i < SlotCount; i++) _items[i] = ItemStack.Empty;
        if (tag.GetList("Items") is not { } items) return;
        foreach (var element in items)
        {
            if (element is not CompoundTag entry) continue;
            var slot = entry.GetByteOr("Slot", (byte)SlotCount);
            if (slot >= SlotCount) continue;
            _items[slot] = ItemStack.ReadNbt(entry.GetCompound("item"));
        }
    }

    //OnRemoved 被移出世界前把三格内容物丢在原地 对应原版 AbstractFurnaceBlockEntity 继承的 preRemoveSideEffects
    public override void OnRemoved()
    {
        if (Level is PersistentServerLevel level) Containers.DropContents(level, Pos, this);
    }

    //IsItem 物品栈是否是指定注册名的物品 湿海绵与水桶那几条特判用
    private static bool IsItem(ItemStack stack, string name)
        => !stack.IsEmpty() && stack.GetItem().Id == Identifier.WithDefaultNamespace(name);

    //CreateStack 按注册名造单个物品栈 注册表里没有该物品时返回空栈
    private static ItemStack CreateStack(string name)
    {
        var item = BuiltInRegistries.ITEM.GetValue(Identifier.WithDefaultNamespace(name));
        return item is null || ReferenceEquals(item, NCItems.AIR)
            ? ItemStack.Empty
            : new ItemStack(item.BuiltInRegistryHolder, 1, DataComponentPatch.Empty);
    }
}
