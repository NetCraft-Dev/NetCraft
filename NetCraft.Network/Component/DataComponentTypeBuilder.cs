using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentTypeBuilder DataComponentType 构造器对应原版 DataComponentType.Builder
//persistent 设 Codec 持久化编解码 networkSynchronized 设 StreamCodec 网络同步编解码
//cacheEncoding 启用编码缓存 ignoreSwapAnimation 忽略交换动画
//build 时未设 StreamCodec 则用 fromCodecWithRegistries 从 Codec 派生暂未实现抛异常
public sealed class DataComponentTypeBuilder<T> where T : class
{
    private Codec<T>? _codec;
    private StreamCodec<RegistryFriendlyByteBuf, T>? _streamCodec;
    private bool _cacheEncoding;
    private bool _ignoreSwapAnimation;

    //Persistent 设持久化 Codec
    public DataComponentTypeBuilder<T> Persistent(Codec<T> codec)
    {
        _codec = codec;
        return this;
    }

    //NetworkSynchronized 设网络同步 StreamCodec
    public DataComponentTypeBuilder<T> NetworkSynchronized(StreamCodec<RegistryFriendlyByteBuf, T> streamCodec)
    {
        _streamCodec = streamCodec;
        return this;
    }

    //CacheEncoding 启用编码缓存
    public DataComponentTypeBuilder<T> CacheEncoding()
    {
        _cacheEncoding = true;
        return this;
    }

    //IgnoreSwapAnimation 忽略交换动画
    public DataComponentTypeBuilder<T> IgnoreSwapAnimation()
    {
        _ignoreSwapAnimation = true;
        return this;
    }

    //Build 构造 SimpleDataComponentType
    //未设 StreamCodec 时从 Codec 派生暂未实现抛 NotSupportedException
    public DataComponentType<T> Build()
    {
        var streamCodec = _streamCodec ?? FromCodecWithRegistries(_codec);
        return new SimpleDataComponentType<T>(_codec, streamCodec, _ignoreSwapAnimation);
    }

    //FromCodecWithRegistries 从 Codec 派生 StreamCodec 暂未实现
    //实际 DataComponentType 都显式 NetworkSynchronized 不走此路径
    private static StreamCodec<RegistryFriendlyByteBuf, T> FromCodecWithRegistries(Codec<T>? codec)
    {
        if (codec is null)
            throw new InvalidOperationException("Missing Codec for component");
        throw new NotSupportedException("从 Codec 派生 RegistryFriendlyByteBuf StreamCodec 暂未实现");
    }
}
