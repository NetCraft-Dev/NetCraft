using NetCraft.Codec;

namespace NetCraft.Registry;

//TypedDataComponent a typed component value, maps to vanilla net.minecraft.core.component.TypedDataComponent
//The record holds DataComponentType<T> and a T value with immutable value-type semantics
//Stream encoding/decoding depends on the Network layer's registry id read/write, placed in TypedDataComponentCodecs
public sealed record TypedDataComponent<T>(DataComponentType<T> Type, T Value) where T : class
{
    //CreateUnchecked skips the generic check when the type is known safe, maps to vanilla createUnchecked
    public static TypedDataComponent<T> CreateUnchecked(DataComponentType<T> type, object value)
        => new(type, (T)value);

    //FromEntryUnchecked builds an entry from a non-generic key/value, maps to vanilla fromEntryUnchecked
    public static TypedDataComponent<object> FromEntryUnchecked(object type, object value)
        => new((DataComponentType<object>)type, value);

    //EncodeValue encodes with the type's persistent codec, maps to vanilla encodeValue
    public DataResult<D> EncodeValue<D>(DynamicOps<D> ops)
        => Type.Codec is { } codec
            ? codec.EncodeStart(ops, Value)
            : DataResult<D>.Error(() => $"{Type} is not an encodable component");

    public override string ToString() => $"{Type}=>{Value}";
}
