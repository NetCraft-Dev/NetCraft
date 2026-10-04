using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//IEnvironmentAttribute 环境属性的弱类型视图，供注册表与属性映射表统一存取
public interface IEnvironmentAttribute
{
    bool IsSyncable { get; }

    bool IsPositional { get; }

    bool IsSpatiallyInterpolated { get; }

    //DecodeEntry 解析该属性在 JSON 中的条目
    DataResult<EnvironmentAttributeMap.Entry> DecodeEntry<U>(DynamicOps<U> ops, U input);

    //EncodeEntry 编码该属性的条目
    DataResult<U> EncodeEntry<U>(DynamicOps<U> ops, EnvironmentAttributeMap.Entry entry);
}

//EnvironmentAttribute 环境属性定义对应原版 EnvironmentAttribute
public sealed class EnvironmentAttribute<Value> : IEnvironmentAttribute
{
    private Codec<Value>? _valueCodec;
    private Codec<EnvironmentAttributeMap.Entry<Value>>? _entryCodec;

    public AttributeType<Value> Type { get; }

    public Value DefaultValue { get; }

    public AttributeRange<Value> ValueRange { get; }

    public bool IsSyncable { get; }

    public bool IsPositional { get; }

    public bool IsSpatiallyInterpolated { get; }

    private EnvironmentAttribute(AttributeType<Value> type, Value defaultValue, AttributeRange<Value> valueRange,
        bool isSyncable, bool isPositional, bool isSpatiallyInterpolated)
    {
        Type = type;
        DefaultValue = defaultValue;
        ValueRange = valueRange;
        IsSyncable = isSyncable;
        IsPositional = isPositional;
        IsSpatiallyInterpolated = isSpatiallyInterpolated;
    }

    public static Builder<Value> CreateBuilder(AttributeType<Value> type) => new(type);

    //ValueCodec 值 codec 叠加取值区间校验
    public Codec<Value> ValueCodec => _valueCodec ??= new ValidatingCodec<Value>(Type.ValueCodec, ValueRange.Validate);

    public Value SanitizeValue(Value value) => ValueRange.Sanitize(value);

    private Codec<EnvironmentAttributeMap.Entry<Value>> EntryCodec
        => _entryCodec ??= new EnvironmentAttributeEntryCodec<Value>(this);

    public DataResult<EnvironmentAttributeMap.Entry> DecodeEntry<U>(DynamicOps<U> ops, U input)
        => EntryCodec.Parse(ops, input).Map(entry => (EnvironmentAttributeMap.Entry)entry);

    public DataResult<U> EncodeEntry<U>(DynamicOps<U> ops, EnvironmentAttributeMap.Entry entry)
        => entry is EnvironmentAttributeMap.Entry<Value> typed
            ? EntryCodec.EncodeStart(ops, typed)
            : DataResult<U>.Error(() => "Entry value type does not match environment attribute");

    public override string ToString()
    {
        var id = BuiltInRegistries.ENVIRONMENT_ATTRIBUTE.GetKey(this);
        return id is null ? "unregistered" : id.Value.ToString();
    }

    //Builder 环境属性构建器，默认不联网同步、参与位置采样、不空间插值
    public sealed class Builder<TValue>
    {
        private readonly AttributeType<TValue> _type;
        private TValue? _defaultValue;
        private bool _hasDefaultValue;
        private AttributeRange<TValue> _valueRange = AttributeRange<TValue>.Any();
        private bool _isSyncable;
        private bool _isPositional = true;
        private bool _isSpatiallyInterpolated;

        internal Builder(AttributeType<TValue> type) { _type = type; }

        public Builder<TValue> DefaultValue(TValue defaultValue)
        {
            _defaultValue = defaultValue;
            _hasDefaultValue = true;
            return this;
        }

        public Builder<TValue> ValueRange(AttributeRange<TValue> valueRange)
        {
            _valueRange = valueRange;
            return this;
        }

        public Builder<TValue> Syncable()
        {
            _isSyncable = true;
            return this;
        }

        public Builder<TValue> NotPositional()
        {
            _isPositional = false;
            return this;
        }

        public Builder<TValue> SpatiallyInterpolated()
        {
            _isSpatiallyInterpolated = true;
            return this;
        }

        public EnvironmentAttribute<TValue> Build()
        {
            if (!_hasDefaultValue)
                throw new InvalidOperationException("Missing default value");
            return new EnvironmentAttribute<TValue>(_type, _defaultValue!, _valueRange, _isSyncable, _isPositional, _isSpatiallyInterpolated);
        }
    }
}
