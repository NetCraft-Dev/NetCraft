using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//AttributeType 环境属性类型，描述值的 codec 与允许的修饰符对应原版 AttributeType
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

    //OfInterpolated 值可插值的类型，本移植只保留数据侧属性
    public static AttributeType<Value> OfInterpolated(Codec<Value> valueCodec,
        IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>> modifierLibrary)
        => new(valueCodec, modifierLibrary);

    //OfNotInterpolated 值不插值的类型，无库时只允许 override
    public static AttributeType<Value> OfNotInterpolated(Codec<Value> valueCodec,
        IReadOnlyDictionary<AttributeModifier.OperationId, AttributeModifier<Value, object>>? modifierLibrary = null)
        => new(valueCodec, modifierLibrary ?? EmptyLibrary);

    //CheckAllowedModifier 校验修饰符是否属于该类型
    public void CheckAllowedModifier(AttributeModifier<Value, object> modifier)
    {
        if (ReferenceEquals(modifier, AttributeModifier.Override<Value>())) return;
        if (ModifierLibrary.Values.Any(existing => ReferenceEquals(existing, modifier))) return;
        throw new ArgumentException($"Modifier {modifier} is not valid for this attribute type");
    }

    //GetOperationId 反查修饰符对应的操作标识，找不到返回 null
    public AttributeModifier.OperationId? GetOperationId(AttributeModifier<Value, object> modifier)
    {
        if (ReferenceEquals(modifier, AttributeModifier.Override<Value>())) return AttributeModifier.OperationId.Override;
        foreach (var (id, existing) in ModifierLibrary)
            if (ReferenceEquals(existing, modifier)) return id;
        return null;
    }

    //ModifierCodecImpl 按操作标识名解析修饰符，override 恒可用其余必须在该类型的库里
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
