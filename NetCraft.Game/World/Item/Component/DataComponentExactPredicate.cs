using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//DataComponentExactPredicate exact component predicate, every expected entry must equal the target value, maps to vanilla DataComponentExactPredicate
//Lives in the Network layer because both AsPatch and stream coding use the component patch facilities here
public sealed class DataComponentExactPredicate
{
    //CODEC persistence codec, dispatching the value codec by component type, maps to vanilla CODEC
    public static readonly Codec<DataComponentExactPredicate> CODEC =
        DataComponentType<object>.VALUE_MAP_CODEC.ComapFlatMap(
            map =>
            {
                var list = new List<TypedDataComponent<object>>(map.Count);
                foreach (var kv in map) list.Add(new TypedDataComponent<object>(kv.Key, kv.Value));
                return DataResult<DataComponentExactPredicate>.Success(new DataComponentExactPredicate(list));
            },
            predicate => ToValueMap(predicate));

    //StreamCodec network codec, entry list in and out, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, DataComponentExactPredicate> StreamCodec
        = new DataComponentExactPredicateStreamCodec();

    //Empty empty predicate, always true
    public static readonly DataComponentExactPredicate Empty = new(new List<TypedDataComponent<object>>());

    private readonly IReadOnlyList<TypedDataComponent<object>> _expected;

    internal DataComponentExactPredicate(IReadOnlyList<TypedDataComponent<object>> expected) => _expected = expected;

    internal IReadOnlyList<TypedDataComponent<object>> Expected => _expected;

    //NewBuilder creates a predicate builder, maps to vanilla builder
    public static Builder NewBuilder() => new();

    //Expect is a single expectation, maps to vanilla expect
    public static DataComponentExactPredicate Expect(DataComponentType<object> type, object value)
        => new(new[] { new TypedDataComponent<object>(type, value) });

    //AllOf takes all of the target's components as expectations, maps to vanilla allOf
    public static DataComponentExactPredicate AllOf(DataComponentMap components)
    {
        var list = new List<TypedDataComponent<object>>();
        foreach (var key in components.KeySet)
            if (key is DataComponentType<object> type && components.Get(type) is { } value)
                list.Add(new TypedDataComponent<object>(type, value));
        return new DataComponentExactPredicate(list);
    }

    //SomeOf only picks components of the given types as expectations and skips when the target lacks one, maps to vanilla someOf
    public static DataComponentExactPredicate SomeOf(DataComponentMap components, params DataComponentType<object>[] types)
    {
        var builder = NewBuilder();
        foreach (var type in types)
            if (components.Get(type) is { } value) builder.Expect(type, value);
        return builder.Build();
    }

    //IsEmpty has no expectations
    public bool IsEmpty => _expected.Count == 0;

    //AlwaysMatches an empty predicate is always true, maps to vanilla alwaysMatches
    public bool AlwaysMatches => _expected.Count == 0;

    //Test checks whether the target component set satisfies all expectations, maps to vanilla test
    public bool Test(DataComponentGetter components)
    {
        foreach (var expected in _expected)
            if (!Equals(expected.Value, components.Get(expected.Type))) return false;
        return true;
    }

    //AsPatch turns the expectations into a patch, maps to vanilla asPatch
    public DataComponentPatch AsPatch()
    {
        var builder = DataComponentPatch.NewBuilder();
        foreach (var entry in _expected) builder.Set(entry);
        return builder.Build();
    }

    //Equality is by the expectation list, maps to vanilla equals
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

    //ToValueMap takes the persistable expectations, filtering out transient ones on the encode side, maps to the encode side of vanilla CODEC
    private static Dictionary<DataComponentType<object>, object> ToValueMap(DataComponentExactPredicate predicate)
    {
        var map = new Dictionary<DataComponentType<object>, object>();
        foreach (var entry in predicate._expected)
            if (!entry.Type.IsTransient) map[entry.Type] = entry.Value;
        return map;
    }

    //Builder predicate builder, rejecting duplicate types outright, maps to vanilla Builder
    public sealed class Builder
    {
        private readonly List<TypedDataComponent<object>> _expected = new();

        //Expect appends an expectation
        public Builder Expect(DataComponentType<object> type, object value)
        {
            foreach (var entry in _expected)
                if (ReferenceEquals(entry.Type, type))
                    throw new ArgumentException($"an expectation of this type already exists: {type}");
            _expected.Add(new TypedDataComponent<object>(type, value));
            return this;
        }

        public DataComponentExactPredicate Build()
            => new(new List<TypedDataComponent<object>>(_expected));
    }
}

//DataComponentExactPredicateStreamCodec expectation list in and out, maps to vanilla STREAM_CODEC
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
