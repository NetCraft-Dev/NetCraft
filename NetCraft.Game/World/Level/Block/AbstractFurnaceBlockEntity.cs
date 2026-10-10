using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Network.Chat;
using NetCraft.Game.World.Items.Component;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;
using NCItems = NetCraft.Game.World.Items.Items;

namespace NetCraft.Game.World.Level.Block;

//AbstractFurnaceBlockEntity smelting block entity base class, maps to vanilla net.minecraft.world.level.block.entity.AbstractFurnaceBlockEntity
//A three-slot container (input/fuel/result) plus four data slots (remaining burn/current burn total/cooking progress/cooking total)
//The furnace, blast furnace and smoker share it, they differ only in recipe type and menu type
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

    //BurnCoolSpeed cooking progress rollback per tick after the fire goes out, maps to vanilla BURN_COOL_SPEED
    private const int BurnCoolSpeed = 2;

    //DefaultCookTime fallback cooking time when no recipe is found, maps to the 200 fallback of vanilla getTotalCookTime
    private const int DefaultCookTime = 200;

    //MaxStackSize block entity container stack limit, maps to the 64 of vanilla AbstractContainerBlockEntity
    private const int MaxStackSize = 64;

    //ContainerDistanceSqr squared menu invalidation distance, 8 blocks in vanilla
    private const double ContainerDistanceSqr = 64.0;

    private readonly ItemStack[] _items = new ItemStack[SlotCount];
    //_litTimeRemaining remaining burn ticks and _litTotalTime total ticks of the current fuel, together they drive the flame icon progress
    private int _litTimeRemaining;
    private int _litTotalTime;
    //_cookingTimer current cooking progress and _cookingTotalTime ticks the current recipe needs
    private int _cookingTimer;
    private int _cookingTotalTime;

    protected AbstractFurnaceBlockEntity(BlockEntityType type, BlockPos pos) : base(type, pos)
    {
        for (var i = 0; i < SlotCount; i++) _items[i] = ItemStack.Empty;
    }

    //CookingRecipeType recipe type used by this smelting block: furnace smelting, blast furnace blasting, smoker smoking
    public abstract string CookingRecipeType { get; }

    //IsLit whether it is currently burning, for diagnostics and tests
    public bool IsLit => _litTimeRemaining > 0;

    //CookingProgress current cooking progress
    public int CookingProgress => _cookingTimer;

    public abstract Component DisplayName { get; }

    public abstract AbstractContainerMenu CreateMenu(int containerId, PlayerInventory inventory, ServerPlayer player);

    //StillValid valid while the block remains and the player is within 8 blocks, maps to vanilla Container.stillValidBlockEntity
    public bool StillValid(ServerPlayer player)
    {
        if (Level is not ServerLevel level) return false;
        if (level.GetBlockEntity<AbstractFurnaceBlockEntity>(Pos) is not { } current) return false;
        if (!ReferenceEquals(current, this)) return false;
        var center = new Vec3(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
        return player.Position.DistanceToSqr(center) <= ContainerDistanceSqr;
    }

    //Tick advances one batch per tick, maps to vanilla AbstractFurnaceBlockEntity.serverTick
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
            //Burning only matters when there is input; with nothing to cook vanilla just zeroes the cooking progress without resetting the recipe total
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

    //FindRecipe looks up a recipe by this block's type and the input item, null when nothing hits
    private AbstractCookingRecipe? FindRecipe(ItemStack ingredient)
        => RecipeManager.Active?.GetCookingRecipe(CookingRecipeType, ingredient);

    //ConsumeFuel burns one fuel item and swaps in its remainder when used up (lava bucket becomes an empty bucket), maps to vanilla consumeFuel
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

    //CanBurn whether the result slot can take one more output, maps to vanilla canBurn
    private bool CanBurn(ItemStack burnResult)
    {
        var resultSlot = _items[SlotResult];
        if (resultSlot.IsEmpty()) return true;
        if (!resultSlot.IsSameItemAndComponentsAs(burnResult)) return false;
        var resultCount = resultSlot.GetCount() + burnResult.GetCount();
        var maxResultCount = Math.Min(MaxStackSize, burnResult.GetMaxStackSize());
        return resultCount <= maxResultCount;
    }

    //Burn produces one result and consumes one input; a wet sponge with an empty bucket bakes into a water bucket, maps to vanilla burn
    private void Burn(ItemStack ingredient, ItemStack result)
    {
        var resultSlot = _items[SlotResult];
        if (resultSlot.IsEmpty()) _items[SlotResult] = result.Copy();
        else resultSlot.SetCount(resultSlot.GetCount() + result.GetCount());
        if (IsItem(ingredient, "wet_sponge") && !_items[SlotFuel].IsEmpty() && IsItem(_items[SlotFuel], "bucket"))
            _items[SlotFuel] = CreateStack("water_bucket");
        ingredient.Shrink(1);
    }

    //GetTotalCookTime cooking time of the recipe for this input, 200 when not found, maps to vanilla getTotalCookTime
    private int GetTotalCookTime()
    {
        var recipe = FindRecipe(_items[SlotInput]);
        return recipe?.CookingTime ?? DefaultCookTime;
    }

    public int Size => SlotCount;

    public ItemStack GetItem(int slot) => (uint)slot < SlotCount ? _items[slot] : ItemStack.Empty;

    //SetItem writes a slot; changing the input slot to another item recomputes the cooking time and zeroes the progress, maps to vanilla setItem
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

    //SetChanged content changes here rely on the menu scanning slots each tick and periodic full saves, so there is no extra dirty flag to set
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

    //CanPlaceItem the result slot is output-only and the fuel slot accepts only fuel or empty buckets, maps to vanilla canPlaceItem
    public bool CanPlaceItem(int slot, ItemStack stack)
    {
        if (slot == SlotResult) return false;
        if (slot != SlotFuel) return true;
        if (FuelValues.Active?.IsFuel(stack) == true) return true;
        return IsItem(stack, "bucket") && !IsItem(_items[SlotFuel], "bucket");
    }

    public int Count => DataCount;

    //Get reads a data slot, maps to the anonymous ContainerData implementation in vanilla
    public int Get(int index) => index switch
    {
        DataLitTime => _litTimeRemaining,
        DataLitDuration => _litTotalTime,
        DataCookingProgress => _cookingTimer,
        DataCookingTotalTime => _cookingTotalTime,
        _ => 0,
    };

    //Set writes a data slot; values synced from the client go through here and cannot corrupt the server-authoritative logic
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

    //SaveAdditional save field names match vanilla, slots are written in the Slot/item structure of vanilla ContainerHelper
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

    //OnRemoved drops the three slots' contents in place before leaving the world, maps to preRemoveSideEffects inherited by vanilla AbstractFurnaceBlockEntity
    public override void OnRemoved()
    {
        if (Level is PersistentServerLevel level) Containers.DropContents(level, Pos, this);
    }

    //IsItem whether the stack is the item with the given registry name, used by the wet sponge and bucket special cases
    private static bool IsItem(ItemStack stack, string name)
        => !stack.IsEmpty() && stack.GetItem().Id == Identifier.WithDefaultNamespace(name);

    //CreateStack builds a single item stack from a registry name, returns an empty stack when the registry has no such item
    private static ItemStack CreateStack(string name)
    {
        var item = BuiltInRegistries.ITEM.GetValue(Identifier.WithDefaultNamespace(name));
        return item is null || ReferenceEquals(item, NCItems.AIR)
            ? ItemStack.Empty
            : new ItemStack(item.BuiltInRegistryHolder, 1, DataComponentPatch.Empty);
    }
}
