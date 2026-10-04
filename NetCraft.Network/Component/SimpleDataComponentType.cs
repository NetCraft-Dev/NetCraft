using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//SimpleDataComponentType DataComponentType 实现对应原版 DataComponentType.Builder.SimpleType
//持有 Codec 持久化编解码与 StreamCodec 网络同步编解码
//实现 IDataComponentTypeCodec 暴露非泛型 EncodeValue/DecodeValue 供 DataComponentPatch 跨泛型编解码
public sealed class SimpleDataComponentType<T> : DataComponentType<T>, IDataComponentTypeCodec where T : class
{
    //Codec 持久化编解码器 null 表示非持久化 transient 组件
    public Codec<T>? Codec { get; }

    //IgnoreSwapAnimation 是否忽略交换动画
    public bool IgnoreSwapAnimation { get; }

    //StreamCodec 网络同步编解码器 RegistryFriendlyByteBuf 读写
    public StreamCodec<RegistryFriendlyByteBuf, T> StreamCodec { get; }

    public SimpleDataComponentType(Codec<T>? codec, StreamCodec<RegistryFriendlyByteBuf, T> streamCodec, bool ignoreSwapAnimation)
    {
        Codec = codec;
        StreamCodec = streamCodec;
        IgnoreSwapAnimation = ignoreSwapAnimation;
    }

    //EncodeValue 非泛型编码 value cast 为 T 后委托 StreamCodec
    public void EncodeValue(RegistryFriendlyByteBuf buf, object value)
        => StreamCodec.Encode(buf, (T)value);

    //DecodeValue 非泛型解码返回 object
    public object DecodeValue(RegistryFriendlyByteBuf buf)
        => StreamCodec.Decode(buf);

    public override string ToString() => $"DataComponentType[{Codec?.GetType().Name ?? "transient"}]";
}
