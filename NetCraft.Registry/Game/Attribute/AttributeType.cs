using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//AttributeType environment attribute type describing the value codec and allowed modifiers, maps to vanilla AttributeType
public sealed class AttributeType<Value>
{
    private static readonly IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>> EmptyLibrary =
        new Dictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>>();

    public Codec<Value> ValueCodec { get; }

    public IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>> ModifierLibrary { get; }

    public Codec<AttributeModifier<Value, object>> ModifierCodec { get; }

    private AttributeType(Codec<Value> valueCodec,
        IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>> modifierLibrary)
    {
        ValueCodec = valueCodec;
        ModifierLibrary = modifierLibrary;
        ModifierCodec = new ModifierCodecImpl(this);
    }

    //OfInterpolated interpolatable value type; this port keeps only the data-side attributes
    public static AttributeType<Value> OfInterpolated(Codec<Value> valueCodec,
        IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>> modifierLibrary)
        => new(valueCodec, modifierLibrary);

    //OfNotInterpolated non-interpolatable value type; with no library only override is allowed
    public static AttributeType<Value> OfNotInterpolated(Codec<Value> valueCodec,
        IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>>? modifierLibrary = null)
        => new(valueCodec, modifierLibrary ?? EmptyLibrary);

    //CheckAllowedModifier checks whether the modifier belongs to this type
    public void CheckAllowedModifier(AttributeModifier<Value, object> modifier)
    {
        if (ReferenceEquals(modifier, AttributeModifier.Override<Value>())) return;
        if (ModifierLibrary.Values.Any(existing => ReferenceEquals(existing, modifier))) return;
        throw new ArgumentException($"Modifier {modifier} is not valid for this attribute type");
    }

    //GetOperationId reverse-looks up the modifier's operation id, returning null if not found
    public AttributeModifier.OperationId? GetOperationId(AttributeModifier<Value, object> modifier)
    {
        if (ReferenceEquals(modifier, AttributeModifier.Override<Value>())) return AttributeModifier.OperationId.Override;
        foreach (var (id, existing) in ModifierLibrary)
            if (ReferenceEquals(existing, modifier)) return id;
        return null;
    }

    //ModifierCodecImpl resolves a modifier by operation id name; override is always available and the rest must be in the type's library
    private sealed class ModifierCodecImpl : ScalarCodec<AttributeModifier<Value, object>>
    {
        private readonly AttributeType<Value> _type;

        public ModifierCodecImpl(AttributeType<Value> type) { _type = type; }

        public override DataResult<AttributeModifier<Value, object>> Parse<U>(DynamicOps<U> ops, U input)
            => AttributeModifier.OperationIdCodec.Parse(ops, input).FlatMap(id =>
            {
                if (id == AttributeModifier.OperationId.Override)
                    return DataResult<AttributeModifier<Value, object>>.Success(AttributeModifier.Override<Value>());
                return _type.ModifierLibrary.TryGetValue(id, out var modifier)
                    ? DataResult<AttributeModifier<Value, object>>.Success(modifier)
                    : DataResult<AttributeModifier<Value, object>>.Error(() => $"Modifier {id} is not valid for this attribute type");
            });

        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, AttributeModifier<Value, object> value)
        {
            var id = _type.GetOperationId(value);
            return id is null
                ? DataResult<U>.Error(() => "Unknown modifier")
                : AttributeModifier.OperationIdCodec.EncodeStart(ops, id.Value);
        }
    }
}
