using NetCraft.Registry;

namespace NetCraft.Network.Component;

//TypedDataComponentCodecs 带类型组件条目的流编解码 对应原版 TypedDataComponent.STREAM_CODEC
//先写注册表 id 再按该类型的流编解码写值
//放 Network 层是因为要在 RegistryFriendlyByteBuf 上读写注册表 id
public static class TypedDataComponentCodecs
{
    public static readonly StreamCodec<RegistryFriendlyByteBuf, TypedDataComponent<object>> StreamCodec
        = new TypedDataComponentStreamCodec();
}

//TypedDataComponentStreamCodec 条目流编解码实现
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
