using NetCraft.Registry;

namespace NetCraft.Network.Component;

//TypedDataComponentCodecs stream codec for typed component entries, maps to vanilla TypedDataComponent.STREAM_CODEC
//Writes the registry id first, then the value via that type's stream codec
//Lives in the Network layer because it reads and writes registry ids on RegistryFriendlyByteBuf
public static class TypedDataComponentCodecs
{
    public static readonly StreamCodec<RegistryFriendlyByteBuf, TypedDataComponent<object>> StreamCodec
        = new TypedDataComponentStreamCodec();
}

//TypedDataComponentStreamCodec entry stream codec implementation
internal sealed class TypedDataComponentStreamCodec : StreamCodec<RegistryFriendlyByteBuf, TypedDataComponent<object>>
{
    public TypedDataComponent<object> Decode(RegistryFriendlyByteBuf buf)
    {
        var type = (DataComponentType<object>)DataComponentTypeCodecs.Decode(buf);
        var value = ((IDataComponentTypeCodec)type).DecodeValue(buf);
        return new TypedDataComponent<object>(type, value);
    }

    public void Encode(RegistryFriendlyByteBuf buf, TypedDataComponent<object> value)
    {
        DataComponentTypeCodecs.Encode(buf, value.Type);
        ((IDataComponentTypeCodec)value.Type).EncodeValue(buf, value.Value);
    }
}
