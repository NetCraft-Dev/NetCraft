using NetCraft.Codec;

namespace NetCraft.Registry;

//DataComponentType data component type, maps to vanilla net.minecraft.core.component.DataComponentType
//The interface defines only the Codec for persistence; StreamCodec is implemented by SimpleDataComponentType in the Network sub-library
//Builder.persistent sets the Codec, networkSynchronized sets the StreamCodec, and build returns SimpleDataComponentType
//IsTransient means there is no Codec: not persisted, only network-synced
public interface DataComponentType<T>
{
    //Codec persistence codec; null means a non-persistent component
    Codec<T>? Codec { get; }

    //IgnoreSwapAnimation whether to ignore the swap animation
    bool IgnoreSwapAnimation { get; }

    //IsTransient whether non-persistent; a null Codec means transient
    bool IsTransient => Codec is null;

    //CodecOrThrow gets the Codec and throws for non-persistent components
    Codec<T> CodecOrThrow()
    {
        if (Codec is null)
            throw new InvalidOperationException($"{this} is not a persistent component");
        return Codec;
    }

    //CODEC resolves a component type by registry name, maps to vanilla DataComponentType.CODEC
    public static readonly Codec<DataComponentType<object>> CODEC = new ComponentTypeByNameCodec();

    //PERSISTENT_CODEC same as CODEC but rejects transient components, maps to vanilla PERSISTENT_CODEC
    public static readonly Codec<DataComponentType<object>> PERSISTENT_CODEC = CODEC.ComapFlatMap(
        type => type.IsTransient
            ? DataResult<DataComponentType<object>>.Error(
                () => $"Encountered non-persistent component {BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(type)}")
            : DataResult<DataComponentType<object>>.Success(type),
        type => type);

    //VALUE_MAP_CODEC encodes/decodes a component-type-to-value map, each type using its own codec, maps to vanilla VALUE_MAP_CODEC
    public static readonly Codec<Dictionary<DataComponentType<object>, object>> VALUE_MAP_CODEC
        = Codecs.DispatchedMap(PERSISTENT_CODEC, type => type.CodecOrThrow());
}

//ComponentTypeByNameCodec resolves and writes back a component type by registry name, maps to vanilla BuiltInRegistries.DATA_COMPONENT_TYPE.byNameCodec
internal sealed class ComponentTypeByNameCodec : ScalarCodec<DataComponentType<object>>
{
    public override DataResult<DataComponentType<object>> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<DataComponentType<object>>.Error(() => "Component type must be a string");
        var id = Identifier.TryParse(text.GetOrThrow());
        if (id is null)
            return DataResult<DataComponentType<object>>.Error(() => $"Invalid component type name {text.GetOrThrow()}");
        return BuiltInRegistries.DATA_COMPONENT_TYPE.GetValue(id.Value) is DataComponentType<object> type
            ? DataResult<DataComponentType<object>>.Success(type)
            : DataResult<DataComponentType<object>>.Error(() => $"Unknown component type {id}");
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, DataComponentType<object> value)
    {
        var key = BuiltInRegistries.DATA_COMPONENT_TYPE.GetKey(value);
        return key is null
            ? DataResult<U>.Error(() => "Component type not registered, cannot encode")
            : DataResult<U>.Success(ops.CreateString(key.Value.ToString()));
    }
}
