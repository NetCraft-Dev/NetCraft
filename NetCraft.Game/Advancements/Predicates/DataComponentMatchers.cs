using NetCraft.Codec;
using NetCraft.Network.Component;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//DataComponentMatchers 组件匹配组 精确期望加逐类谓词 对应原版 net.minecraft.advancements.predicates.DataComponentMatchers
//精确部分要求逐项相等 谓词部分每项各自判定
public sealed record DataComponentMatchers(
    DataComponentExactPredicate Exact,
    Dictionary<DataComponentPredicate.Type, DataComponentPredicate> Partial)
{
    //Codec 持久化编解码 字段名 components 与 predicates 对应原版 CODEC
    public static readonly Codec<DataComponentMatchers> Codec = RecordCodecBuilder.Of2(
        DataComponentExactPredicate.CODEC.OptionalFieldOf("components", DataComponentExactPredicate.Empty)
            .ForGetter((DataComponentMatchers matchers) => matchers.Exact),
        DataComponentPredicate.CODEC
            .OptionalFieldOf("predicates", new Dictionary<DataComponentPredicate.Type, DataComponentPredicate>())
            .ForGetter((DataComponentMatchers matchers) => matchers.Partial),
        (exact, partial) => new DataComponentMatchers(exact, partial));

    //IsEmpty 精确期望与逐类谓词都为空 对应原版 isEmpty
    public bool IsEmpty => Exact.IsEmpty && Partial.Count == 0;

    //Test 目标组件集是否同时满足精确期望与全部谓词
    public bool Test(DataComponentGetter components)
    {
        if (!Exact.Test(components)) return false;
        foreach (var predicate in Partial.Values)
            if (!predicate.Matches(components)) return false;
        return true;
    }
}
