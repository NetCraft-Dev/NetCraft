namespace NetCraft.Network.Component;

//IDataComponentTypeCodec non-generic codec interface
//SimpleDataComponentType<T> implements this interface so DataComponentPatch.STREAM_CODEC can code component values across generics
//Works around C# generic invariance preventing DataComponentType<T> from unifying to DataComponentType<object>
public interface IDataComponentTypeCodec
{
    //EncodeValue non-generic encode; the actual type of value is cast by the implementer
    void EncodeValue(RegistryFriendlyByteBuf buf, object value);

    //DecodeValue non-generic decode returning object, cast by the caller as needed
    object DecodeValue(RegistryFriendlyByteBuf buf);
}
