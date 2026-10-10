using NetCraft.Codec;
using NetCraft.Game.World.Items;

namespace NetCraft.Game.World.Loot;

//LootPool one weighted draw group inside a loot table, maps to vanilla LootPool
public sealed class LootPool
{
    public static readonly Codec<LootPool> CODEC =
        RecordCodecBuilder.Of5<LootPool, IReadOnlyList<LootPoolEntryContainer>, IReadOnlyList<LootItemCondition>, IReadOnlyList<LootItemFunction>, NumberProvider, NumberProvider>(
            LootPoolEntries.CODEC.ListOf().FieldOf("entries")
                .ForGetter<LootPool, IReadOnlyList<LootPoolEntryContainer>>(p => p.Entries),
            LootItemConditions.TYPED_CODEC.ListOf().OptionalFieldOf("conditions", Array.Empty<LootItemCondition>())
                .ForGetter<LootPool, IReadOnlyList<LootItemCondition>>(p => p.Conditions),
            LootItemFunctions.TYPED_CODEC.ListOf().OptionalFieldOf("functions", Array.Empty<LootItemFunction>())
                .ForGetter<LootPool, IReadOnlyList<LootItemFunction>>(p => p.Functions),
            NumberProviders.CODEC.FieldOf("rolls").ForGetter<LootPool, NumberProvider>(p => p.Rolls),
            NumberProviders.CODEC.OptionalFieldOf("bonus_rolls", ConstantValue.Exactly(0f))
                .ForGetter<LootPool, NumberProvider>(p => p.BonusRolls),
            (entries, conditions, functions, rolls, bonusRolls) => new LootPool(entries, conditions, functions, rolls, bonusRolls));

    private readonly Func<ItemStack, LootContext, ItemStack> _compositeFunction;

    private LootPool(IReadOnlyList<LootPoolEntryContainer> entries, IReadOnlyList<LootItemCondition> conditions,
        IReadOnlyList<LootItemFunction> functions, NumberProvider rolls, NumberProvider bonusRolls)
    {
        Entries = entries;
        Conditions = conditions;
        Functions = functions;
        Rolls = rolls;
        BonusRolls = bonusRolls;
        _compositeFunction = LootItemFunctions.Compose(functions);
    }

    public IReadOnlyList<LootPoolEntryContainer> Entries { get; }

    public IReadOnlyList<LootItemCondition> Conditions { get; }

    public IReadOnlyList<LootItemFunction> Functions { get; }

    public NumberProvider Rolls { get; }

    public NumberProvider BonusRolls { get; }

    //AddRandomItems checks the pool conditions then rolls the pool, maps to vanilla addRandomItems
    public void AddRandomItems(Action<ItemStack> output, LootContext context)
    {
        foreach (var condition in Conditions)
            if (!condition.Test(context)) return;
        var decorated = LootItemFunctions.Decorate(_compositeFunction, output, context);
        var count = Rolls.GetInt(context) + (int)MathF.Floor(BonusRolls.GetFloat(context) * context.Luck);
        for (var i = 0; i < count; i++)
            AddRandomItem(decorated, context);
    }

    //AddRandomItem picks one entry by weight and produces from it, maps to vanilla addRandomItem
    private void AddRandomItem(Action<ItemStack> output, LootContext context)
    {
        var valid = new List<LootPoolEntry>();
        var totalWeight = 0;
        foreach (var entry in Entries)
            entry.Expand(context, e =>
            {
                var weight = e.GetWeight(context.Luck);
                if (weight <= 0) return;
                valid.Add(e);
                totalWeight += weight;
            });
        if (totalWeight == 0 || valid.Count == 0) return;
        if (valid.Count == 1)
        {
            valid[0].CreateItemStack(output, context);
            return;
        }
        var index = context.Random.NextInt(totalWeight);
        foreach (var entry in valid)
        {
            index -= entry.GetWeight(context.Luck);
            if (index < 0)
            {
                entry.CreateItemStack(output, context);
                return;
            }
        }
    }
}
