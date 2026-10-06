using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//DataComponentMatchers component matcher group, exact expectations plus per-type predicates, maps to vanilla net.minecraft.advancements.predicates.DataComponentMatchers
//The exact part requires item-by-item equality; each predicate part is evaluated on its own
public sealed record DataComponentMatchers(
    DataComponentExactPredicate Exact,
    Dictionary<DataComponentPredicate.Type, DataComponentPredicate> Partial)
{
    //Codec persistence codec, field names components/predicates, maps to vanilla CODEC
    public static readonly Codec<DataComponentMatchers> Codec = RecordCodecBuilder.Of2(
        DataComponentExactPredicate.CODEC.OptionalFieldOf("components", DataComponentExactPredicate.Empty)
            .ForGetter((DataComponentMatchers matchers) => matchers.Exact),
        DataComponentPredicate.CODEC
            .OptionalFieldOf("predicates", new Dictionary<DataComponentPredicate.Type, DataComponentPredicate>())
            .ForGetter((DataComponentMatchers matchers) => matchers.Partial),
        (exact, partial) => new DataComponentMatchers(exact, partial));

    //IsEmpty both the exact expectations and per-type predicates are empty, maps to vanilla isEmpty
    public bool IsEmpty => Exact.IsEmpty && Partial.Count == 0;

    //Test whether the target component set satisfies both the exact expectations and all predicates
    public bool Test(DataComponentGetter components)
    {
        if (!Exact.Test(components)) return false;
        foreach (var predicate in Partial.Values)
            if (!predicate.Matches(components)) return false;
        return true;
    }
}
