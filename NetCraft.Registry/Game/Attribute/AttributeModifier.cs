using NetCraft.Codec;

namespace NetCraft.Registry.Environment;

//AttributeModifier 属性修饰符，把 argument 应用到主体值上对应原版 AttributeModifier
public interface AttributeModifier<Subject, Argument>
{
    Subject Apply(Subject subject, Argument argument);

    Codec<Argument> ArgumentCodec(EnvironmentAttribute<Subject> attribute);
}

//AttributeModifier 静态入口，提供 override 与各值类型的修饰符库对应原版 AttributeModifier
public static class AttributeModifier
{
    //OperationId 修饰符操作标识对应原版 AttributeModifier.OperationId
    public enum OperationId
    {
        Override,
        AlphaBlend,
        Add,
        Subtract,
        Multiply,
        BlendToGray,
        Minimum,
        Maximum,
        And,
        Nand,
        Or,
        Nor,
        Xor,
        Xnor
    }

    //OperationIdCodec 按字符串名编解码操作标识
    public static readonly Codec<OperationId> OperationIdCodec = new OperationIdCodecImpl();

    private static readonly Dictionary<string, OperationId> NameToOperation = new()
    {
        ["override"] = OperationId.Override,
        ["alpha_blend"] = OperationId.AlphaBlend,
        ["add"] = OperationId.Add,
        ["subtract"] = OperationId.Subtract,
        ["multiply"] = OperationId.Multiply,
        ["blend_to_gray"] = OperationId.BlendToGray,
        ["minimum"] = OperationId.Minimum,
        ["maximum"] = OperationId.Maximum,
        ["and"] = OperationId.And,
        ["nand"] = OperationId.Nand,
        ["or"] = OperationId.Or,
        ["nor"] = OperationId.Nor,
        ["xor"] = OperationId.Xor,
        ["xnor"] = OperationId.Xnor
    };

    //GetSerializedName 取操作标识的存档名
    public static string GetSerializedName(OperationId id) => id switch
    {
        OperationId.Override => "override",
        OperationId.AlphaBlend => "alpha_blend",
        OperationId.Add => "add",
        OperationId.Subtract => "subtract",
        OperationId.Multiply => "multiply",
        OperationId.BlendToGray => "blend_to_gray",
        OperationId.Minimum => "minimum",
        OperationId.Maximum => "maximum",
        OperationId.And => "and",
        OperationId.Nand => "nand",
        OperationId.Or => "or",
        OperationId.Nor => "nor",
        OperationId.Xor => "xor",
        OperationId.Xnor => "xnor",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    public static bool TryFromName(string name, out OperationId id) => NameToOperation.TryGetValue(name, out id);

    //Override 直接用参数覆盖主体值
    public static AttributeModifier<Value, object> Override<Value>() => OverrideModifier<Value>.Instance;

    public static readonly IReadOnlyDictionary<OperationId, AttributeModifier<bool, object>> BooleanLibrary =
        new Dictionary<OperationId, AttributeModifier<bool, object>>
        {
            [OperationId.And] = new ObjectArgumentModifier<bool, bool>(BooleanModifier.And),
            [OperationId.Nand] = new ObjectArgumentModifier<bool, bool>(BooleanModifier.Nand),
            [OperationId.Or] = new ObjectArgumentModifier<bool, bool>(BooleanModifier.Or),
            [OperationId.Nor] = new ObjectArgumentModifier<bool, bool>(BooleanModifier.Nor),
            [OperationId.Xor] = new ObjectArgumentModifier<bool, bool>(BooleanModifier.Xor),
            [OperationId.Xnor] = new ObjectArgumentModifier<bool, bool>(BooleanModifier.Xnor)
        };

    public static readonly IReadOnlyDictionary<OperationId, AttributeModifier<float, object>> FloatLibrary =
        new Dictionary<OperationId, AttributeModifier<float, object>>
        {
            [OperationId.AlphaBlend] = new ObjectArgumentModifier<float, FloatWithAlpha>(FloatModifier.AlphaBlend),
            [OperationId.Add] = new ObjectArgumentModifier<float, float>(FloatModifier.Add),
            [OperationId.Subtract] = new ObjectArgumentModifier<float, float>(FloatModifier.Subtract),
            [OperationId.Multiply] = new ObjectArgumentModifier<float, float>(FloatModifier.Multiply),
            [OperationId.Minimum] = new ObjectArgumentModifier<float, float>(FloatModifier.Minimum),
            [OperationId.Maximum] = new ObjectArgumentModifier<float, float>(FloatModifier.Maximum)
        };

    public static readonly IReadOnlyDictionary<OperationId, AttributeModifier<int, object>> RgbColorLibrary =
        new Dictionary<OperationId, AttributeModifier<int, object>>
        {
            [OperationId.AlphaBlend] = new ObjectArgumentModifier<int, int>(ColorModifier.AlphaBlend),
            [OperationId.Add] = new ObjectArgumentModifier<int, int>(ColorModifier.Add),
            [OperationId.Subtract] = new ObjectArgumentModifier<int, int>(ColorModifier.Subtract),
            [OperationId.Multiply] = new ObjectArgumentModifier<int, int>(ColorModifier.MultiplyRgb),
            [OperationId.BlendToGray] = new ObjectArgumentModifier<int, BlendToGray>(ColorModifier.BlendToGray)
        };

    public static readonly IReadOnlyDictionary<OperationId, AttributeModifier<int, object>> ArgbColorLibrary =
        new Dictionary<OperationId, AttributeModifier<int, object>>
        {
            [OperationId.AlphaBlend] = new ObjectArgumentModifier<int, int>(ColorModifier.AlphaBlend),
            [OperationId.Add] = new ObjectArgumentModifier<int, int>(ColorModifier.Add),
            [OperationId.Subtract] = new ObjectArgumentModifier<int, int>(ColorModifier.Subtract),
            [OperationId.Multiply] = new ObjectArgumentModifier<int, int>(ColorModifier.MultiplyArgb),
            [OperationId.BlendToGray] = new ObjectArgumentModifier<int, BlendToGray>(ColorModifier.BlendToGray)
        };

    public static readonly IReadOnlyDictionary<OperationId, AttributeModifier<int, object>> IntegerLibrary =
        new Dictionary<OperationId, AttributeModifier<int, object>>
        {
            [OperationId.Add] = new ObjectArgumentModifier<int, int>(IntegerModifier.Add),
            [OperationId.Subtract] = new ObjectArgumentModifier<int, int>(IntegerModifier.Subtract),
            [OperationId.Multiply] = new ObjectArgumentModifier<int, int>(IntegerModifier.Multiply),
            [OperationId.Minimum] = new ObjectArgumentModifier<int, int>(IntegerModifier.Minimum),
            [OperationId.Maximum] = new ObjectArgumentModifier<int, int>(IntegerModifier.Maximum)
        };
}

//OverrideModifier 恒返回参数的修饰符对应原版 AttributeModifier.OverrideModifier
internal sealed class OverrideModifier<Value> : AttributeModifier<Value, object>
{
    public static readonly OverrideModifier<Value> Instance = new();

    public Value Apply(Value subject, object argument) => (Value)argument;

    public Codec<object> ArgumentCodec(EnvironmentAttribute<Value> attribute)
        => new ObjectValueCodec<Value>(attribute.ValueCodec);
}

//ObjectArgumentModifier 把具体 Argument 类型擦除为 object 以便放进修饰符库
internal sealed class ObjectArgumentModifier<Subject, Argument> : AttributeModifier<Subject, object>
{
    private readonly AttributeModifier<Subject, Argument> _inner;

    public ObjectArgumentModifier(AttributeModifier<Subject, Argument> inner) { _inner = inner; }

    public Subject Apply(Subject subject, object argument) => _inner.Apply(subject, (Argument)argument);

    public Codec<object> ArgumentCodec(EnvironmentAttribute<Subject> attribute)
        => new ObjectValueCodec<Argument>(_inner.ArgumentCodec(attribute));
}

//OperationIdCodecImpl 操作标识的字符串编解码
internal sealed class OperationIdCodecImpl : ScalarCodec<AttributeModifier.OperationId>
{
    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, AttributeModifier.OperationId value)
        => DataResult<U>.Success(ops.CreateString(AttributeModifier.GetSerializedName(value)));

    public override DataResult<AttributeModifier.OperationId> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetStringValue(input).FlatMap(name => AttributeModifier.TryFromName(name, out var id)
            ? DataResult<AttributeModifier.OperationId>.Success(id)
            : DataResult<AttributeModifier.OperationId>.Error(() => $"Unknown operation: {name}"));
}
