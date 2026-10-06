using NetCraft.Codec;
using NetCraft.Registry.Codec;

namespace NetCraft.Registry.Environment;

//EnvironmentAttributeMap a set of environment attribute values, maps to vanilla EnvironmentAttributeMap
public sealed class EnvironmentAttributeMap
{
    public static readonly EnvironmentAttributeMap Empty =
        new(new Dictionary<IEnvironmentAttribute, Entry>());

    //Codec dispatches and parses by attribute id, maps to vanilla dispatchedMap
    public static readonly Codec<EnvironmentAttributeMap> Codec = new EnvironmentAttributeMapCodec();

    //NetworkCodec keeps only syncable attributes
    public static readonly Codec<EnvironmentAttributeMap> NetworkCodec = new NetworkFilterCodec(Codec);

    //CodecOnlyPositional validates that all attributes participate in positional sampling
    public static readonly Codec<EnvironmentAttributeMap> CodecOnlyPositional =
        new ValidatingCodec<EnvironmentAttributeMap>(Codec, ValidatePositional);

    private readonly Dictionary<IEnvironmentAttribute, Entry> _entries;

    internal EnvironmentAttributeMap(Dictionary<IEnvironmentAttribute, Entry> entries) { _entries = entries; }

    internal IReadOnlyDictionary<IEnvironmentAttribute, Entry> Entries => _entries;

    public static Builder CreateBuilder() => new();

    public Entry<Value>? Get<Value>(EnvironmentAttribute<Value> attribute)
        => _entries.TryGetValue(attribute, out var entry) ? entry as Entry<Value> : null;

    //ApplyModifier returns the base value when the attribute is absent
    public Value ApplyModifier<Value>(EnvironmentAttribute<Value> attribute, Value baseValue)
    {
        var entry = Get(attribute);
        return entry is null ? baseValue : entry.ApplyModifier(baseValue);
    }

    public bool Contains(IEnvironmentAttribute attribute) => _entries.ContainsKey(attribute);

    public IReadOnlyCollection<IEnvironmentAttribute> KeySet => _entries.Keys;

    public override string ToString()
        => string.Join(", ", _entries.Select(kv => $"{kv.Key}={kv.Value.Argument}"));

    private static DataResult<EnvironmentAttributeMap> ValidatePositional(EnvironmentAttributeMap map)
    {
        var illegal = map._entries.Keys.Where(attribute => !attribute.IsPositional).ToList();
        return illegal.Count == 0
            ? DataResult<EnvironmentAttributeMap>.Success(map)
            : DataResult<EnvironmentAttributeMap>.Error(() => $"The following attributes cannot be positional: {string.Join(", ", illegal)}");
    }

    //FilterSyncable removes non-syncable attributes
    internal EnvironmentAttributeMap FilterSyncable()
        => new(_entries.Where(kv => kv.Key.IsSyncable).ToDictionary(kv => kv.Key, kv => kv.Value));

    //Entry weak-typed entry base class
    public abstract class Entry
    {
        public abstract object Argument { get; }

        internal abstract object ApplyModifierObject(object subject);
    }

    //Entry<Value> a single attribute value; the argument type is erased to object
    public sealed class Entry<Value> : Entry
    {
        public override object Argument { get; }

        public AttributeModifier<Value, object> Modifier { get; }

        public Entry(object argument, AttributeModifier<Value, object> modifier)
        {
            Argument = argument;
            Modifier = modifier;
        }

        public Value ApplyModifier(Value subject) => Modifier.Apply(subject, Argument);

        internal override object ApplyModifierObject(object subject) => ApplyModifier((Value)subject)!;
    }

    //Builder attribute map builder
    public sealed class Builder
    {
        private readonly Dictionary<IEnvironmentAttribute, Entry> _entries = new();

        public Builder PutAll(EnvironmentAttributeMap map)
        {
            foreach (var (attribute, entry) in map._entries)
                _entries[attribute] = entry;
            return this;
        }

        public Builder Modify<Value>(EnvironmentAttribute<Value> attribute,
            AttributeModifier<Value, object> modifier, object argument)
        {
            attribute.Type.CheckAllowedModifier(modifier);
            _entries[attribute] = new Entry<Value>(argument, modifier);
            return this;
        }

        public Builder Set<Value>(EnvironmentAttribute<Value> attribute, Value value)
            => Modify(attribute, AttributeModifier.Override<Value>(), value!);

        public EnvironmentAttributeMap Build()
            => _entries.Count == 0 ? Empty : new EnvironmentAttributeMap(new Dictionary<IEnvironmentAttribute, Entry>(_entries));
    }
}

//EnvironmentAttributeMapCodec looks up the registry by attribute id then parses the entry
internal sealed class EnvironmentAttributeMapCodec : AbstractMapCodec<EnvironmentAttributeMap>
{
    public override DataResult<EnvironmentAttributeMap> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var entries = new Dictionary<IEnvironmentAttribute, EnvironmentAttributeMap.Entry>();
        foreach (var (key, value) in input.Entries())
        {
            var idResult = IdentifierCodec.Instance.Parse(ops, key);
            if (!idResult.Result().IsPresent)
                return DataResult<EnvironmentAttributeMap>.Error(() => "Environment attribute key is not a valid identifier");
            var id = idResult.GetOrThrow();
            var attribute = BuiltInRegistries.ENVIRONMENT_ATTRIBUTE.GetValue(id);
            if (attribute is null)
                return DataResult<EnvironmentAttributeMap>.Error(() => $"Unknown environment attribute: {id}");
            var entryResult = attribute.DecodeEntry(ops, value);
            if (!entryResult.Result().IsPresent)
                return DataResult<EnvironmentAttributeMap>.Error(() => $"Failed to parse environment attribute: {id}");
            entries[attribute] = entryResult.GetOrThrow();
        }
        return DataResult<EnvironmentAttributeMap>.Success(new EnvironmentAttributeMap(entries));
    }

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, EnvironmentAttributeMap value, RecordBuilder<U> builder)
    {
        foreach (var (attribute, entry) in value.Entries)
        {
            var id = BuiltInRegistries.ENVIRONMENT_ATTRIBUTE.GetKey(attribute);
            if (id is null) continue;
            builder.Add(id.Value.ToString(), attribute.EncodeEntry(ops, entry).GetOrThrow());
        }
        return builder;
    }
}

//NetworkFilterCodec removes non-syncable attributes during network sync, maps to vanilla NetworkCodec
internal sealed class NetworkFilterCodec : ScalarCodec<EnvironmentAttributeMap>
{
    private readonly Codec<EnvironmentAttributeMap> _inner;

    public NetworkFilterCodec(Codec<EnvironmentAttributeMap> inner) { _inner = inner; }

    public override DataResult<EnvironmentAttributeMap> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(map => map.FilterSyncable());

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, EnvironmentAttributeMap value)
        => _inner.EncodeStart(ops, value.FilterSyncable());
}

//EnvironmentAttributeEntryCodec entry codec; the shorthand gives the value directly and the full form carries modifier and argument
internal sealed class EnvironmentAttributeEntryCodec<Value> : ScalarCodec<EnvironmentAttributeMap.Entry<Value>>
{
    private readonly EnvironmentAttribute<Value> _attribute;

    public EnvironmentAttributeEntryCodec(EnvironmentAttribute<Value> attribute) { _attribute = attribute; }

    public override DataResult<EnvironmentAttributeMap.Entry<Value>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var simple = _attribute.ValueCodec.Parse(ops, input);
        if (simple.Result().IsPresent)
            return DataResult<EnvironmentAttributeMap.Entry<Value>>.Success(
                new EnvironmentAttributeMap.Entry<Value>(simple.GetOrThrow()!, AttributeModifier.Override<Value>()));
        return ParseFull(ops, input);
    }

    private DataResult<EnvironmentAttributeMap.Entry<Value>> ParseFull<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map =>
        {
            var modifierInput = map.Get("modifier");
            if (!modifierInput.IsPresent)
                return DataResult<EnvironmentAttributeMap.Entry<Value>>.Error(() => "Missing key modifier");
            var argumentInput = map.Get("argument");
            if (!argumentInput.IsPresent)
                return DataResult<EnvironmentAttributeMap.Entry<Value>>.Error(() => "Missing key argument");
            return _attribute.Type.ModifierCodec.Parse(ops, modifierInput.Get()).FlatMap(modifier =>
                modifier.ArgumentCodec(_attribute).Parse(ops, argumentInput.Get())
                    .Map(argument => new EnvironmentAttributeMap.Entry<Value>(argument!, modifier)));
        });

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, EnvironmentAttributeMap.Entry<Value> value)
    {
        if (ReferenceEquals(value.Modifier, AttributeModifier.Override<Value>()))
            return _attribute.ValueCodec.EncodeStart(ops, (Value)value.Argument);
        var operationId = _attribute.Type.GetOperationId(value.Modifier);
        if (operationId is null)
            return DataResult<U>.Error(() => "Unknown modifier");
        var builder = ops.MapBuilder();
        builder.Add("modifier", AttributeModifier.OperationIdCodec.EncodeStart(ops, operationId.Value).GetOrThrow());
        builder.Add("argument", value.Modifier.ArgumentCodec(_attribute).EncodeStart(ops, value.Argument).GetOrThrow());
        return builder.Build(ops.Empty());
    }
}
