using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Items;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry.State;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.World.Level.Block;

//CampfireBlockEntity 营火方块实体 对应原版 net.minecraft.world.level.block.entity.CampfireBlockEntity
//四格各自独立烹饪 每格自己记进度 烤好直接把成品掉在地上 没有界面也没有燃料槽
public sealed class CampfireBlockEntity : BlockEntity
{
    public const int SlotCount = 4;

    //BurnCoolSpeed 熄灭后烹饪进度每刻回退量 对应原版 BURN_COOL_SPEED
    private const int BurnCoolSpeed = 2;

    private readonly ItemStack[] _items = new ItemStack[SlotCount];
    private readonly int[] _cookingProgress = new int[SlotCount];
    private readonly int[] _cookingTime = new int[SlotCount];

    public CampfireBlockEntity(BlockPos pos) : base(BlockEntityTypes.CAMPFIRE, pos)
    {
        for (var i = 0; i < SlotCount; i++) _items[i] = ItemStack.Empty;
    }

    //Items 四格内容 供渲染同步与诊断读取
    public IReadOnlyList<ItemStack> Items => _items;

    public ItemStack GetItem(int slot) => (uint)slot < SlotCount ? _items[slot] : ItemStack.Empty;

    //GetCookingProgress 某格当前烹饪进度
    public int GetCookingProgress(int slot) => (uint)slot < SlotCount ? _cookingProgress[slot] : 0;

    //GetCookingTotalTime 某格所需烹饪刻数
    public int GetCookingTotalTime(int slot) => (uint)slot < SlotCount ? _cookingTime[slot] : 0;

    //PlaceFood 往第一个空格放一份食物 对应原版 placeFood
    //该物品没有营火配方就放不进去 放成功返回 true
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

    //Tick 点燃时推进 熄灭时回退 对应原版 CampfireBlockEntity.tick 的两个分支
    public override void Tick()
    {
        if (Level is not PersistentServerLevel level) return;
        var state = level.GetBlockState(Pos);
        var lit = state is not null && state.Value.GetValue(BlockStateProperties.Lit);
        if (lit) CookTick(level);
        else CooldownTick();
    }

    //CookTick 每格进度加一 到点把成品掉在方块位置并清空该格 对应原版 cookTick
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
            //内容变了才通知客户端 进度本身不发包 免得每刻刷一次方块实体数据
            level.BlockEntityChanged(Pos);
        }
    }

    //CooldownTick 熄灭后进度按 BURN_COOL_SPEED 回退 对应原版 cooldownTick
    private void CooldownTick()
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (_cookingProgress[slot] <= 0) continue;
            _cookingProgress[slot] = Mth.Clamp(_cookingProgress[slot] - BurnCoolSpeed, 0, _cookingTime[slot]);
        }
    }

    //SaveAdditional 落盘字段名与原版一致 槽位按 Slot/item 结构写
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
