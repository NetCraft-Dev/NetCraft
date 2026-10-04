using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//FuelValues 燃料燃烧时长表 对应原版 net.minecraft.world.level.block.entity.FuelValues
//原版把物品与物品标签展开成一张 物品->燃烧刻数 的表 由数据包加载完成后的关卡持有
//本作按 FuelValues.Active 静态持有 与 RecipeManager.Active 同一套做法
public sealed class FuelValues
{
    //Active 当前生效的燃料表 数据重载后重建 熔炉烧燃料时读它
    public static FuelValues? Active { get; set; }

    private readonly Dictionary<Item, int> _values = new();

    private FuelValues(Dictionary<Item, int> values) => _values = values;

    //IsFuel 该物品能不能当燃料 对应原版 isFuel
    public bool IsFuel(ItemStack stack)
        => !stack.IsEmpty() && _values.ContainsKey(stack.GetItem());

    //BurnDuration 该物品烧多少刻 非燃料返回 0 对应原版 burnDuration
    public int BurnDuration(ItemStack stack)
        => stack.IsEmpty() ? 0 : _values.GetValueOrDefault(stack.GetItem());

    //FuelItemCount 表内燃料种类数 供诊断与测试
    public int FuelItemCount => _values.Count;

    //VanillaBurnTimes 原版内置燃料表 逐项按原版 vanillaBurnTimes 的添加顺序对齐
    //顺序有意义: 同一物品被多条规则命中时以最后一条为准 最后的 remove 也要在末尾执行
    //baseUnit 原版默认 200 刻即一块木板烧一个物品的时间
    public static FuelValues VanillaBurnTimes(int baseUnit = 200)
    {
        var builder = new Builder();
        builder.AddItem("lava_bucket", baseUnit * 100);
        builder.AddItem("coal_block", baseUnit * 8 * 10);
        builder.AddItem("blaze_rod", baseUnit * 12);
        builder.AddItem("coal", baseUnit * 8);
        builder.AddItem("charcoal", baseUnit * 8);
        builder.AddTag("logs", baseUnit * 3 / 2);
        builder.AddTag("bamboo_blocks", baseUnit * 3 / 2);
        builder.AddTag("planks", baseUnit * 3 / 2);
        builder.AddItem("bamboo_mosaic", baseUnit * 3 / 2);
        builder.AddTag("wooden_stairs", baseUnit * 3 / 2);
        builder.AddItem("bamboo_mosaic_stairs", baseUnit * 3 / 2);
        builder.AddTag("wooden_slabs", baseUnit * 3 / 4);
        builder.AddItem("bamboo_mosaic_slab", baseUnit * 3 / 4);
        builder.AddTag("wooden_trapdoors", baseUnit * 3 / 2);
        builder.AddTag("wooden_pressure_plates", baseUnit * 3 / 2);
        builder.AddTag("wooden_shelves", baseUnit * 3 / 2);
        builder.AddTag("wooden_fences", baseUnit * 3 / 2);
        builder.AddTag("fence_gates", baseUnit * 3 / 2);
        builder.AddItem("note_block", baseUnit * 3 / 2);
        builder.AddItem("bookshelf", baseUnit * 3 / 2);
        builder.AddItem("chiseled_bookshelf", baseUnit * 3 / 2);
        builder.AddItem("lectern", baseUnit * 3 / 2);
        builder.AddItem("jukebox", baseUnit * 3 / 2);
        builder.AddItem("chest", baseUnit * 3 / 2);
        builder.AddItem("trapped_chest", baseUnit * 3 / 2);
        builder.AddItem("crafting_table", baseUnit * 3 / 2);
        builder.AddItem("daylight_detector", baseUnit * 3 / 2);
        builder.AddTag("banners", baseUnit * 3 / 2);
        builder.AddItem("bow", baseUnit * 3 / 2);
        builder.AddItem("fishing_rod", baseUnit * 3 / 2);
        builder.AddItem("ladder", baseUnit * 3 / 2);
        builder.AddTag("signs", baseUnit);
        builder.AddTag("hanging_signs", baseUnit * 4);
        builder.AddItem("wooden_shovel", baseUnit);
        builder.AddItem("wooden_sword", baseUnit);
        builder.AddItem("wooden_spear", baseUnit);
        builder.AddItem("wooden_hoe", baseUnit);
        builder.AddItem("wooden_axe", baseUnit);
        builder.AddItem("wooden_pickaxe", baseUnit);
        builder.AddTag("wooden_doors", baseUnit);
        builder.AddTag("boats", baseUnit * 6);
        builder.AddTag("wool", baseUnit / 2);
        builder.AddTag("wooden_buttons", baseUnit / 2);
        builder.AddItem("stick", baseUnit / 2);
        builder.AddTag("saplings", baseUnit / 2);
        builder.AddItem("bowl", baseUnit / 2);
        builder.AddTag("wool_carpets", 1 + baseUnit / 3);
        builder.AddItem("dried_kelp_block", 1 + baseUnit * 20);
        builder.AddItem("crossbow", baseUnit * 3 / 2);
        builder.AddItem("bamboo", baseUnit / 4);
        builder.AddItem("dead_bush", baseUnit / 2);
        builder.AddItem("short_dry_grass", baseUnit / 2);
        builder.AddItem("tall_dry_grass", baseUnit / 2);
        builder.AddItem("scaffolding", baseUnit / 4);
        builder.AddItem("loom", baseUnit * 3 / 2);
        builder.AddItem("barrel", baseUnit * 3 / 2);
        builder.AddItem("cartography_table", baseUnit * 3 / 2);
        builder.AddItem("fletching_table", baseUnit * 3 / 2);
        builder.AddItem("smithing_table", baseUnit * 3 / 2);
        builder.AddItem("composter", baseUnit * 3 / 2);
        builder.AddItem("azalea", baseUnit / 2);
        builder.AddItem("flowering_azalea", baseUnit / 2);
        builder.AddItem("mangrove_roots", baseUnit * 3 / 2);
        builder.AddItem("leaf_litter", baseUnit / 2);
        builder.AddTag("non_flammable_wood", 0, remove: true);
        return builder.Build();
    }

    //Builder 燃料表构建器 物品 id 与标签写进来统一展开成物品->刻数
    private sealed class Builder
    {
        private readonly Dictionary<Item, int> _values = new();

        //AddItem 单个物品入表 未注册的 id 直接跳过
        public void AddItem(string name, int time)
        {
            if (LookupItem(name) is { } item) _values[item] = time;
        }

        //AddTag 整个物品标签入表 标签未加载或未绑定则跳过
        public void AddTag(string tagName, int time, bool remove = false)
        {
            var tag = TagKey<Item>.Create(Registries.ITEM, Identifier.WithDefaultNamespace(tagName));
            if (BuiltInRegistries.ITEM.Get(tag) is not { } set) return;
            foreach (var holder in set)
            {
                if (ReferenceEquals(holder.Value, NetCraft.Game.World.Items.Items.AIR)) continue;
                if (remove) _values.Remove(holder.Value);
                else _values[holder.Value] = time;
            }
        }

        public FuelValues Build() => new(new Dictionary<Item, int>(_values));

        private static Item? LookupItem(string name)
        {
            var item = BuiltInRegistries.ITEM.GetValue(Identifier.WithDefaultNamespace(name));
            return item is null || ReferenceEquals(item, NetCraft.Game.World.Items.Items.AIR) ? null : item;
        }
    }
}
