using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentPredicate 数据组件谓词 对应原版 net.minecraft.core.component.predicates.DataComponentPredicate
//谓词判给定的组件集是否满足条件 每种谓词一个 Type 由 DATA_COMPONENT_PREDICATE_TYPE 注册表分派
//谓词值一率装箱成 object 与组件系统同一套约定
public interface DataComponentPredicate
{
    //Matches 目标组件集是否满足本谓词
    bool Matches(DataComponentGetter components);

    //CODEC 类型到谓词的映射编解码 对应原版 CODEC
    public static readonly Codec<Dictionary<Type, object>> CODEC =
        Codecs.DispatchedMap(Type.CODEC, type => type.Codec);

    //Type 谓词类型 对应原版 DataComponentPredicate.Type
    public interface Type : DataComponentPredicateType<object>
    {
        //Codec 该谓词本体的编解码
        Codec<object> Codec { get; }

        //Matches 用该谓词判目标组件集
        bool Matches(DataComponentGetter components, object predicate);

        //CODEC 具体谓词类型或组件类型二者取一 组件类型侧表示"只要有该组件即可" 对应原版 Type.CODEC
        public static readonly Codec<Type> CODEC = new PredicateTypeRefCodec();
    }
}

//PredicateTypeRefCodec 谓词类型引用编解码
//先按谓词类型注册名解析 失败再按组件类型解析 组件类型侧视为存在性谓词
//对应原版 Type.CODEC 里 either(谓词类型, 组件类型) 再统一成 Type 的那条链
internal sealed class PredicateTypeRefCodec : ScalarCodec<DataComponentPredicate.Type>
{
    public override DataResult<DataComponentPredicate.Type> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<DataComponentPredicate.Type>.Error(() => "谓词类型必须是字符串");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<DataComponentPredicate.Type>.Error(() => $"非法的标识符: {text.GetOrThrow()}");
        if (BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE.GetValue(id.Value) is DataComponentPredicate.Type type)
            return DataResult<DataComponentPredicate.Type>.Success(type);
        if (BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id.Value) is DataComponentType<object> componentType)
            return DataResult<DataComponentPredicate.Type>.Success(new AnyValueType(componentType));
        return DataResult<DataComponentPredicate.Type>.Error(() => $"未知的谓词类型或组件类型 {id}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DataComponentPredicate.Type value)
    {
        if (value is AnyValueType anyValue)
        {
            var componentKey = BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(anyValue.ComponentType);
            return componentKey is null
                ? DataResult<U>.Error(() => "组件类型未注册 无法编码")
                : DataResult<U>.Success(ops.CreateString(componentKey.Value.ToString()));
        }
        var key = BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE.GetKey(value);
        return key is null
            ? DataResult<U>.Error(() => "谓词类型未注册 无法编码")
            : DataResult<U>.Success(ops.CreateString(key.Value.ToString()));
    }
}

//ConcreteType 绑一个具体谓词类型的通用实现 对应原版 DataComponentPredicate.ConcreteType
public sealed class ConcreteType<T> : DataComponentPredicate.Type where T : class, DataComponentPredicate
{
    private readonly Codec<object> _boxedCodec;

    public ConcreteType(Codec<T> valueCodec) => _boxedCodec = new ObjectCodecAdapter<T>(valueCodec);

    public Codec<object> Codec => _boxedCodec;

    public bool Matches(DataComponentGetter components, object predicate)
        => ((T)predicate).Matches(components);
}

//AnyValueType 存在性谓词的类型 序列化只写类型名不带值 对应原版 AnyValueType
public sealed class AnyValueType : DataComponentPredicate.Type
{
    public AnyValueType(DataComponentType<object> componentType) => ComponentType = componentType;

    //ComponentType 该存在性谓词盯着的组件类型
    public DataComponentType<object> ComponentType { get; }

    public Codec<object> Codec => new BoundUnitCodec(ComponentType);

    public bool Matches(DataComponentGetter components, object predicate)
        => ((AnyValue)predicate).Matches(components);
}

//AnyValue 存在性谓词 目标只要有该组件就算匹配 对应原版 AnyValue
public sealed record AnyValue(DataComponentType<object> Type) : DataComponentPredicate
{
    public bool Matches(DataComponentGetter components) => components.Get(Type) is not null;
}

//BoundUnitCodec 存在性谓词的编解码 没有内容 解析恒给出该谓词 编码写空 对应原版 MapCodec.unitCodec
internal sealed class BoundUnitCodec : ScalarCodec<object>
{
    private readonly DataComponentType<object> _componentType;

    public BoundUnitCodec(DataComponentType<object> componentType) => _componentType = componentType;

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<object>.Success(new AnyValue(_componentType));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => DataResult<U>.Success(ops.Empty());
}

//ObjectCodecAdapter 把引用类型的 codec 适配成 object 版 供谓词值装箱传递
internal sealed class ObjectCodecAdapter<T> : ScalarCodec<object> where T : class
{
    private readonly Codec<T> _inner;

    public ObjectCodecAdapter(Codec<T> inner) => _inner = inner;

    public override DataResult<object> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(value => (object)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, object value)
        => _inner.EncodeStart(ops, (T)value);
}
