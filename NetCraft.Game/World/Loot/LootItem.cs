using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Items.Component;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Loot;

//LootItem drops a single fixed item, the entry vanilla writes for "minecraft:item", maps to vanilla LootItem
public sealed class LootItem : LootPoolSingletonContainer
{
    public static readonly MapCodec<LootPoolEntryContainer> MAP_CODEC =
        RecordCodecBuilder.Of5<LootPoolEntryContainer, Holder<Item>, int, int, IReadOnlyList<LootItemCondition>, IReadOnlyList<LootItemFunction>>(
            NetCraft.Registry.Item.CODEC.FieldOf("name").ForGetter<LootPoolEntryContainer, Holder<Item>>(e => ((LootItem)e).Item),
            Codecs.Int.OptionalFieldOf("weight", 1).ForGetter<LootPoolEntryContainer, int>(e => ((LootItem)e).Weight),
            Codecs.Int.OptionalFieldOf("quality", 0).ForGetter<LootPoolEntryContainer, int>(e => ((LootItem)e).Quality),
            LootItemConditions.TYPED_CODEC.ListOf().OptionalFieldOf("conditions", Array.Empty<LootItemCondition>())
                .ForGetter<LootPoolEntryContainer, IReadOnlyList<LootItemCondition>>(e => ((LootItem)e).Conditions),
            LootItemFunctions.TYPED_CODEC.ListOf().OptionalFieldOf("functions", Array.Empty<LootItemFunction>())
                .ForGetter<LootPoolEntryContainer, IReadOnlyList<LootItemFunction>>(e => ((LootItem)e).Functions),
            (item, weight, quality, conditions, functions) => new LootItem(item, weight, quality, conditions, functions));

    private LootItem(Holder<Item> item, int weight, int quality, IReadOnlyList<LootItemCondition> conditions,
        IReadOnlyList<LootItemFunction> functions) : base(weight, quality, conditions, functions)
        => Item = item;

    public Holder<Item> Item { get; }

    public override Identifier TypeId => Identifier.WithDefaultNamespace("item");

    protected override void CreateItemStack(Action<ItemStack> output, LootContext context)
        => output(new ItemStack(Item, 1, DataComponentPatch.Empty));
}

//LootPoolEntries the entry type registry plus the root codec, maps to vanilla LootPoolEntries
public static class LootPoolEntries
{
    private static readonly Dictionary<Identifier, MapCodec<LootPoolEntryContainer>> Types = new();

    //CODEC dispatches on the default "type" field, the shape vanilla writes for a pool entry
    public static readonly Codec<LootPoolEntryContainer> CODEC =
        IdentifierCodec.Instance.Dispatch<LootPoolEntryContainer, Identifier>("type", e => e.TypeId, Lookup);

    static LootPoolEntries()
    {
        Register("item", LootItem.MAP_CODEC);
        Register("alternatives", AlternativesEntry.MAP_CODEC);
    }

    private static MapCodec<LootPoolEntryContainer> Lookup(Identifier id)
        => Types.TryGetValue(id, out var codec)
            ? codec
            : throw new KeyNotFoundException($"Unknown loot pool entry type: {id}");

    private static void Register(string name, MapCodec<LootPoolEntryContainer> codec)
        => Types[Identifier.WithDefaultNamespace(name)] = codec;
}
