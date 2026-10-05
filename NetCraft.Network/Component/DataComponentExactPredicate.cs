using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentExactPredicate 精确组件谓词 期望的每项都要与目标值相等 对应原版 DataComponentExactPredicate
//放 Network 层是因为 AsPatch 与流编解码都用这里的组件补丁设施
public sealed class DataComponentExactPredicate
{
    //CODEC 持久化编解码 靠组件类型分派值的 codec 对应原版 CODEC
    public static readonly Codec<DataComponentExactPredicate> CODEC =
        DataComponentType<object>.VALUE_MAP_CODEC.ComapFlatMap(
            map =>
            {
                var list = new List<TypedDataComponent<object>>(map.Count);
                foreach (var kv in map) list.Add(new TypedDataComponent<object>(kv.Key, kv.Value));
                return DataResult<DataComponentExactPredicate>.Success(new DataComponentExactPredicate(list));
            },
            predicate => ToValueMap(predicate));

    //StreamCodec 网络编解码 条目列表进出 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, DataComponentExactPredicate> StreamCodec
        = new DataComponentExactPredicateStreamCodec();

    //Empty 空谓词 恒真
    public static readonly DataComponentExactPredicate Empty = new(new List<TypedDataComponent<object>>());

    private readonly IReadOnlyList<TypedDataComponent<object>> _expected;

    internal DataComponentExactPredicate(IReadOnlyList<TypedDataComponent<object>> expected) => _expected = expected;

    internal IReadOnlyList<TypedDataComponent<object>> Expected => _expected;

    //NewBuilder 造谓词构造器 对应原版 builder
    public static Builder NewBuilder() => new();

    //Expect 单项期望 对应原版 expect
    public static DataComponentExactPredicate Expect(DataComponentType<object> type, object value)
        => new(new[] { new TypedDataComponent<object>(type, value) });

    //AllOf 目标的全部组件都作为期望 对应原版 allOf
    public static DataComponentExactPredicate AllOf(DataComponentMap components)
    {
        var list = new List<TypedDataComponent<object>>();
        foreach (var key in components.KeySet)
            if (key is DataComponentType<object> type && components.Get(type) is { } value)
                list.Add(new TypedDataComponent<object>(type, value));
        return new DataComponentExactPredicate(list);
    }

    //SomeOf 只挑指定类型的组件作为期望 目标缺该项则跳过 对应原版 someOf
    public static DataComponentExactPredicate SomeOf(DataComponentMap components, params DataComponentType<object>[] types)
    {
        var builder = NewBuilder();
        foreach (var type in types)
            if (components.Get(type) is { } value) builder.Expect(type, value);
        return builder.Build();
    }

    //IsEmpty 没有任何期望
    public bool IsEmpty => _expected.Count == 0;

    //AlwaysMatches 空谓词恒真 对应原版 alwaysMatches
    public bool AlwaysMatches => _expected.Count == 0;

    //Test 目标组件集是否满足全部期望 对应原版 test
    public bool Test(DataComponentGetter components)
    {
        foreach (var expected in _expected)
            if (!Equals(expected.Value, components.Get(expected.Type))) return false;
        return true;
    }

    //AsPatch 把期望项转成补丁 对应原版 asPatch
    public DataComponentPatch AsPatch()
    {
        var builder = DataComponentPatch.NewBuilder();
        foreach (var entry in _expected) builder.Set(entry);
        return builder.Build();
    }

    //判等按期望项列表 对应原版 equals
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not DataComponentExactPredicate other || other._expected.Count != _expected.Count) return false;
        for (var i = 0; i < _expected.Count; i++)
            if (!_expected[i].Equals(other._expected[i])) return false;
        return true;
    }

    public override int GetHashCode()
    {
        var hash = 1;
        foreach (var entry in _expected) hash = hash * 31 + entry.GetHashCode();
        return hash;
    }

    public override string ToString() => "[" + string.Join(", ", _expected) + "]";

    //ToValueMap 取可持久化的期望项 编码侧过滤 transient 对应原版 CODEC 的编码侧
    private static Dictionary<DataComponentType<object>, object> ToValueMap(DataComponentExactPredicate predicate)
    {
        var map = new Dictionary<DataComponentType<object>, object>();
        foreach (var entry in predicate._expected)
            if (!entry.Type.IsTransient) map[entry.Type] = entry.Value;
        return map;
    }

    //Builder 谓词构造器 重复类型直接拒绝 对应原版 Builder
    public sealed class Builder
    {
        private readonly List<TypedDataComponent<object>> _expected = new();

        //Expect 追加一项期望
        public Builder Expect(DataComponentType<object> type, object value)
        {
            foreach (var entry in _expected)
                if (ReferenceEquals(entry.Type, type))
                    throw new ArgumentException($"已经存在该类型的期望: {type}");
            _expected.Add(new TypedDataComponent<object>(type, value));
            return this;
        }

        public DataComponentExactPredicate Build()
            => new(new List<TypedDataComponent<object>>(_expected));
    }
}

//DataComponentExactPredicateStreamCodec 期望项列表进出 对应原版 STREAM_CODEC
internal sealed class DataComponentExactPredicateStreamCodec
    : StreamCodec<RegistryFriendlyByteBuf, DataComponentExactPredicate>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, List<TypedDataComponent<object>>> ListCodec
        = ByteBufCodecs.Collection(TypedDataComponentCodecs.StreamCodec);

    public DataComponentExactPredicate Decode(RegistryFriendlyByteBuf buf)
        => new DataComponentExactPredicate(ListCodec.Decode(buf));

    public void Encode(RegistryFriendlyByteBuf buf, DataComponentExactPredicate value)
        => ListCodec.Encode(buf, new List<TypedDataComponent<object>>(value.Expected));
}
