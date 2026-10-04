namespace NetCraft.Network.Component;

//IDataComponentTypeCodec 非泛型编解码接口
//SimpleDataComponentType<T> 实现此接口供 DataComponentPatch.STREAM_CODEC 跨泛型编解码组件值
//绕过 C# 泛型不变性 DataComponentType<T> 无法统一为 DataComponentType<object> 的问题
public interface IDataComponentTypeCodec
{
    //EncodeValue 非泛型编码 value 实际类型由实现方 cast
    void EncodeValue(RegistryFriendlyByteBuf buf, object value);

    //DecodeValue 非泛型解码返回 object 调用方按需 cast
    object DecodeValue(RegistryFriendlyByteBuf buf);
}
