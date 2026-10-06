using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//DataComponentTypeBuilder DataComponentType builder, maps to vanilla DataComponentType.Builder
//persistent sets the Codec persistence codec, networkSynchronized sets the StreamCodec network sync codec
//cacheEncoding enables encode caching, ignoreSwapAnimation ignores the swap animation
//When StreamCodec is unset at build time, deriving from Codec via fromCodecWithRegistries is not yet implemented and throws
public sealed class DataComponentTypeBuilder<T> where T : class
{
    private Codec<T>? _codec;
    private StreamCodec<RegistryFriendlyByteBuf, T>? _streamCodec;
    private bool _cacheEncoding;
    private bool _ignoreSwapAnimation;

    //Persistent sets the persistence Codec
    public DataComponentTypeBuilder<T> Persistent(Codec<T> codec)
    {
        _codec = codec;
        return this;
    }

    //NetworkSynchronized sets the network sync StreamCodec
    public DataComponentTypeBuilder<T> NetworkSynchronized(StreamCodec<RegistryFriendlyByteBuf, T> streamCodec)
    {
        _streamCodec = streamCodec;
        return this;
    }

    //CacheEncoding enables encode caching
    public DataComponentTypeBuilder<T> CacheEncoding()
    {
        _cacheEncoding = true;
        return this;
    }

    //IgnoreSwapAnimation ignores the swap animation
    public DataComponentTypeBuilder<T> IgnoreSwapAnimation()
    {
        _ignoreSwapAnimation = true;
        return this;
    }

    //Build constructs a SimpleDataComponentType
    //When StreamCodec is unset, deriving from Codec is not yet implemented and throws NotSupportedException
    public DataComponentType<T> Build()
    {
        var streamCodec = _streamCodec ?? FromCodecWithRegistries(_codec);
        return new SimpleDataComponentType<T>(_codec, streamCodec, _ignoreSwapAnimation);
    }

    //FromCodecWithRegistries derives a StreamCodec from a Codec, not yet implemented
    //In practice DataComponentTypes always explicitly NetworkSynchronized, so this path is not taken
    private static StreamCodec<RegistryFriendlyByteBuf, T> FromCodecWithRegistries(Codec<T>? codec)
    {
        if (codec is null)
            throw new InvalidOperationException("Missing Codec for component");
        throw new NotSupportedException("deriving a RegistryFriendlyByteBuf StreamCodec from Codec is not yet implemented");
    }
}
