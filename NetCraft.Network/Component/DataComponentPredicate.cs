using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentPredicate data component predicate, maps to vanilla net.minecraft.core.component.predicates.DataComponentPredicate
//A predicate checks whether a given component set satisfies a condition; each predicate has a Type dispatched by the DATA_COMPONENT_PREDICATE_TYPE registry
public interface DataComponentPredicate
{
    //Matches checks whether the target component set satisfies this predicate
    bool Matches(DataComponentGetter components);

    //CODEC map codec from type to predicate, maps to vanilla CODEC
    public static readonly Codec<Dictionary<Type, DataComponentPredicate>> CODEC =
        Codecs.DispatchedMap(Type.CODEC, type => type.Codec);

    //Type predicate type, maps to vanilla DataComponentPredicate.Type
    public interface Type : DataComponentPredicateType<object>
    {
        //Codec codec for the predicate body
        Codec<DataComponentPredicate> Codec { get; }

        //Matches uses the predicate to judge the target component set
        bool Matches(DataComponentGetter components, DataComponentPredicate predicate);

        //CODEC takes either a concrete predicate type or a component type; the component type side means "having the component is enough", maps to vanilla Type.CODEC
        public static readonly Codec<Type> CODEC = new PredicateTypeRefCodec();
    }
}

//ConcreteType a generic implementation bound to a concrete predicate type, maps to vanilla DataComponentPredicate.ConcreteType
public sealed class ConcreteType<T> : DataComponentPredicate.Type where T : class, DataComponentPredicate
{
    private readonly Codec<DataComponentPredicate> _codec;

    public ConcreteType(Codec<T> valueCodec) => _codec = new PredicateCodecAdapter<T>(valueCodec);

    public Codec<DataComponentPredicate> Codec => _codec;

    public bool Matches(DataComponentGetter components, DataComponentPredicate predicate)
        => ((T)predicate).Matches(components);
}

//AnyValueType the type of an existence predicate, serializing only the type name without a value, maps to vanilla AnyValueType
public sealed class AnyValueType : DataComponentPredicate.Type
{
    public AnyValueType(DataComponentType<object> componentType) => ComponentType = componentType;

    //ComponentType the component type this existence predicate watches
    public DataComponentType<object> ComponentType { get; }

    public Codec<DataComponentPredicate> Codec => new BoundUnitCodec(ComponentType);

    public bool Matches(DataComponentGetter components, DataComponentPredicate predicate)
        => predicate.Matches(components);
}

//AnyValue existence predicate, matching as long as the target has the component, maps to vanilla AnyValue
public sealed record AnyValue(DataComponentType<object> Type) : DataComponentPredicate
{
    public bool Matches(DataComponentGetter components) => components.Get(Type) is not null;
}

//BoundUnitCodec codec for an existence predicate with no content: parsing always yields the predicate and encoding writes nothing, maps to vanilla MapCodec.unitCodec
internal sealed class BoundUnitCodec : ScalarCodec<DataComponentPredicate>
{
    private readonly DataComponentType<object> _componentType;

    public BoundUnitCodec(DataComponentType<object> componentType) => _componentType = componentType;

    public override DataResult<DataComponentPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => DataResult<DataComponentPredicate>.Success(new AnyValue(_componentType));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DataComponentPredicate value)
        => DataResult<U>.Success(ops.Empty());
}

//PredicateCodecAdapter adapts a concrete predicate type's codec to the predicate interface for the predicate registry to dispatch
internal sealed class PredicateCodecAdapter<T> : ScalarCodec<DataComponentPredicate>
    where T : class, DataComponentPredicate
{
    private readonly Codec<T> _inner;

    public PredicateCodecAdapter(Codec<T> inner) => _inner = inner;

    public override DataResult<DataComponentPredicate> Parse<U>(DynamicOps<U> ops, U input)
        => _inner.Parse(ops, input).Map(value => (DataComponentPredicate)value);

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DataComponentPredicate value)
        => _inner.EncodeStart(ops, (T)value);
}

//PredicateTypeRefCodec predicate type reference codec
//First resolves by predicate type registry name, then by component type on failure, treating the component type side as an existence predicate
//Maps to the chain in vanilla Type.CODEC that does either(predicate type, component type) and then unifies into Type
internal sealed class PredicateTypeRefCodec : ScalarCodec<DataComponentPredicate.Type>
{
    public override DataResult<DataComponentPredicate.Type> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<DataComponentPredicate.Type>.Error(() => "predicate type must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<DataComponentPredicate.Type>.Error(() => $"invalid identifier: {text.GetOrThrow()}");
        if (BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE.GetValue(id.Value) is DataComponentPredicate.Type type)
            return DataResult<DataComponentPredicate.Type>.Success(type);
        if (BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id.Value) is DataComponentType<object> componentType)
            return DataResult<DataComponentPredicate.Type>.Success(new AnyValueType(componentType));
        return DataResult<DataComponentPredicate.Type>.Error(() => $"unknown predicate type or component type {id}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DataComponentPredicate.Type value)
    {
        if (value is AnyValueType anyValue)
        {
            var componentKey = BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(anyValue.ComponentType);
            return componentKey is null
                ? DataResult<U>.Error(() => "component type not registered, cannot encode")
                : DataResult<U>.Success(ops.CreateString(componentKey.Value.ToString()));
        }
        var key = BuiltInRegistries.DATA_COMPONENT_PREDICATE_TYPE.GetKey(value);
        return key is null
            ? DataResult<U>.Error(() => "predicate type not registered, cannot encode")
            : DataResult<U>.Success(ops.CreateString(key.Value.ToString()));
    }
}
