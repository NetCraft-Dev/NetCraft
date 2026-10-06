using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Level.Block;

//CampfireBlockEntity campfire block entity, maps to vanilla net.minecraft.world.level.block.entity.CampfireBlockEntity
//Four slots cook independently, each tracking its own progress; when done the result drops on the ground; no screen and no fuel slot
public sealed class CampfireBlockEntity : BlockEntity
{
    public const int SlotCount = 4;

    //BurnCoolSpeed amount cooking progress rolls back per tick after being extinguished, maps to vanilla BURN_COOL_SPEED
    private const int BurnCoolSpeed = 2;

    private readonly ItemStack[] _items = new ItemStack[SlotCount];
    private readonly int[] _cookingProgress = new int[SlotCount];
    private readonly int[] _cookingTime = new int[SlotCount];

    public CampfireBlockEntity(BlockPos pos) : base(BlockEntityTypes.CAMPFIRE, pos)
    {
        for (var i = 0; i < SlotCount; i++) _items[i] = ItemStack.Empty;
    }

    //Items the four slot contents, read for render sync and diagnostics
    public IReadOnlyList<ItemStack> Items => _items;

    public ItemStack GetItem(int slot) => (uint)slot < SlotCount ? _items[slot] : ItemStack.Empty;

    //GetCookingProgress current cooking progress of a slot
    public int GetCookingProgress(int slot) => (uint)slot < SlotCount ? _cookingProgress[slot] : 0;

    //GetCookingTotalTime cooking ticks required for a slot
    public int GetCookingTotalTime(int slot) => (uint)slot < SlotCount ? _cookingTime[slot] : 0;

    //PlaceFood places one food into the first empty slot, maps to vanilla placeFood
    //Cannot be placed if the item has no campfire recipe; returns true on success
    public bool PlaceFood(ItemStack stack)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (!_items[slot].IsEmpty()) continue;
            var recipe = RecipeManager.Active?.GetCookingRecipe(CampfireCookingRecipe.SerializerId, stack);
            if (recipe is null) return false;
            _items[slot] = stack.CopyWithCount(1);
            _cookingTime[slot] = recipe.CookingTime;
            _cookingProgress[slot] = 0;
            Level?.BlockEntityChanged(Pos);
            return true;
        }
        return false;
    }

    //Tick advances while lit and rolls back while out, maps to the two branches of vanilla CampfireBlockEntity.tick
    public override void Tick()
    {
        if (Level is not PersistentServerLevel level) return;
        var state = level.GetBlockState(Pos);
        var lit = state is not null && state.Value.GetValue(BlockStateProperties.Lit);
        if (lit) CookTick(level);
        else CooldownTick();
    }

    //CookTick bumps each slot's progress by one; when due, drops the result at the block and clears the slot, maps to vanilla cookTick
    private void CookTick(PersistentServerLevel level)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var stack = _items[slot];
            if (stack.IsEmpty()) continue;
            _cookingProgress[slot]++;
            if (_cookingProgress[slot] < _cookingTime[slot]) continue;
            var recipe = RecipeManager.Active?.GetCookingRecipe(CampfireCookingRecipe.SerializerId, stack);
            var result = recipe is null ? stack : recipe.Assemble(new SingleRecipeInput(stack));
            ServerBlockUpdates.SpawnDrop(level, Pos, result);
            _items[slot] = ItemStack.Empty;
            //Notify the client only when contents change; progress itself is not sent, avoiding a block entity data refresh every tick
            level.BlockEntityChanged(Pos);
        }
    }

    //CooldownTick rolls progress back by BURN_COOL_SPEED after being extinguished, maps to vanilla cooldownTick
    private void CooldownTick()
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (_cookingProgress[slot] <= 0) continue;
            _cookingProgress[slot] = Mth.Clamp(_cookingProgress[slot] - BurnCoolSpeed, 0, _cookingTime[slot]);
        }
    }

    //SaveAdditional disk field names match vanilla; slots written as Slot/item entries
    public override void SaveAdditional(CompoundTag tag)
    {
        base.SaveAdditional(tag);
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
        tag.PutIntArray("CookingTimes", _cookingProgress);
        tag.PutIntArray("CookingTotalTimes", _cookingTime);
    }

    public override void LoadAdditional(CompoundTag tag)
    {
        base.LoadAdditional(tag);
        for (var i = 0; i < SlotCount; i++)
        {
            _items[i] = ItemStack.Empty;
            _cookingProgress[i] = 0;
            _cookingTime[i] = 0;
        }
        if (tag.GetList("Items") is { } items)
        {
            foreach (var element in items)
            {
                if (element is not CompoundTag entry) continue;
                var slot = entry.GetByteOr("Slot", (byte)SlotCount);
                if (slot >= SlotCount) continue;
                _items[slot] = ItemStack.ReadNbt(entry.GetCompound("item"));
            }
        }
        if (tag.GetIntArray("CookingTimes")?.Value is { } progress)
            Array.Copy(progress, _cookingProgress, Math.Min(SlotCount, progress.Length));
        if (tag.GetIntArray("CookingTotalTimes")?.Value is { } totalTimes)
            Array.Copy(totalTimes, _cookingTime, Math.Min(SlotCount, totalTimes.Length));
    }
}
