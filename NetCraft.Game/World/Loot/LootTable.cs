using NetCraft.Codec;
using NetCraft.Game.World.Items;
using NetCraft.Registry.Context;

namespace NetCraft.Game.World.Loot;

//LootTable a set of pools resolved together, maps to vanilla LootTable
public sealed class LootTable
{
    public static readonly Codec<LootTable> CODEC =
        RecordCodecBuilder.Of3<LootTable, ContextKeySet, IReadOnlyList<LootPool>, IReadOnlyList<LootItemFunction>>(
            LootContextParamSets.CODEC.OptionalFieldOf("type", LootContextParamSets.AllParams)
                .ForGetter<LootTable, ContextKeySet>(t => t.ParamSet),
            LootPool.CODEC.ListOf().OptionalFieldOf("pools", Array.Empty<LootPool>())
                .ForGetter<LootTable, IReadOnlyList<LootPool>>(t => t.Pools),
            LootItemFunctions.TYPED_CODEC.ListOf().OptionalFieldOf("functions", Array.Empty<LootItemFunction>())
                .ForGetter<LootTable, IReadOnlyList<LootItemFunction>>(t => t.Functions),
            (paramSet, pools, functions) => new LootTable(paramSet, pools, functions));

    private readonly Func<ItemStack, LootContext, ItemStack> _compositeFunction;

    private LootTable(ContextKeySet paramSet, IReadOnlyList<LootPool> pools, IReadOnlyList<LootItemFunction> functions)
    {
        ParamSet = paramSet;
        Pools = pools;
        Functions = functions;
        _compositeFunction = LootItemFunctions.Compose(functions);
    }

    public ContextKeySet ParamSet { get; }

    public IReadOnlyList<LootPool> Pools { get; }

    public IReadOnlyList<LootItemFunction> Functions { get; }

    //GetRandomItems resolves the table and collects what it produced, maps to vanilla getRandomItems
    public List<ItemStack> GetRandomItems(LootParams parameters)
        => GetRandomItems(new LootContext.Builder(parameters).Create());

    public List<ItemStack> GetRandomItems(LootContext context)
    {
        var result = new List<ItemStack>();
        GetRandomItems(context, result.Add);
        return result;
    }

    //GetRandomItems feeds every produced stack to output, refusing to recurse into itself
    public void GetRandomItems(LootContext context, Action<ItemStack> output)
    {
        if (!context.PushVisitedElement(this)) return;
        var decorated = LootItemFunctions.Decorate(_compositeFunction, output, context);
        foreach (var pool in Pools)
            pool.AddRandomItems(decorated, context);
        context.PopVisitedElement(this);
    }
}
