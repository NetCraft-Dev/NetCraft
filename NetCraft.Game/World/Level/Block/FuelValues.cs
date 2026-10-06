using NetCraft.Game.World.Items;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Block;

//FuelValues fuel burn duration table, maps to vanilla net.minecraft.world.level.block.entity.FuelValues
//Vanilla expands items and item tags into an item -> burn ticks table, held by the level after datapacks are loaded
//Here it is held statically as FuelValues.Active, same approach as RecipeManager.Active
public sealed class FuelValues
{
    //Active currently effective fuel table, rebuilt after data reload, read by furnaces when burning fuel
    public static FuelValues? Active { get; set; }

    private readonly Dictionary<Item, int> _values = new();

    private FuelValues(Dictionary<Item, int> values) => _values = values;

    //IsFuel whether the item can be used as fuel, maps to vanilla isFuel
    public bool IsFuel(ItemStack stack)
        => !stack.IsEmpty() && _values.ContainsKey(stack.GetItem());

    //BurnDuration burn ticks for the item, 0 for non-fuel, maps to vanilla burnDuration
    public int BurnDuration(ItemStack stack)
        => stack.IsEmpty() ? 0 : _values.GetValueOrDefault(stack.GetItem());

    //FuelItemCount number of fuel kinds in the table, for diagnostics and tests
    public int FuelItemCount => _values.Count;

    //VanillaBurnTimes vanilla built-in fuel table, each entry aligned with the addition order of vanilla vanillaBurnTimes
    //Order matters: when an item matches several rules the last one wins, and the final remove must also run last
    //baseUnit defaults to 200 ticks in vanilla, the time one plank burns to smelt one item
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

    //Builder fuel table builder; item ids and tags are written in and uniformly expanded into item -> ticks
    private sealed class Builder
    {
        private readonly Dictionary<Item, int> _values = new();

        //AddItem inserts a single item, unregistered ids are skipped
        public void AddItem(string name, int time)
        {
            if (LookupItem(name) is { } item) _values[item] = time;
        }

        //AddTag inserts an entire item tag, skipped if the tag is not loaded or bound
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
