using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//SimpleDataComponentType DataComponentType implementation, maps to vanilla DataComponentType.Builder.SimpleType
//Holds a Codec persistence codec and a StreamCodec network sync codec
//Implements IDataComponentTypeCodec, exposing non-generic EncodeValue/DecodeValue for DataComponentPatch to code across generics
public sealed class SimpleDataComponentType<T> : DataComponentType<T>, IDataComponentTypeCodec where T : class
{
    //Codec persistence codec; null means a non-persistent transient component
    public Codec<T>? Codec { get; }

    //IgnoreSwapAnimation indicates whether to ignore the swap animation
    public bool IgnoreSwapAnimation { get; }

    //StreamCodec network sync codec, reading and writing RegistryFriendlyByteBuf
    public StreamCodec<RegistryFriendlyByteBuf, T> StreamCodec { get; }

    public SimpleDataComponentType(Codec<T>? codec, StreamCodec<RegistryFriendlyByteBuf, T> streamCodec, bool ignoreSwapAnimation)
    {
        Codec = codec;
        StreamCodec = streamCodec;
        IgnoreSwapAnimation = ignoreSwapAnimation;
    }

    //EncodeValue non-generic encode casts value to T and delegates to StreamCodec
    public void EncodeValue(RegistryFriendlyByteBuf buf, object value)
        => StreamCodec.Encode(buf, (T)value);

    //DecodeValue non-generic decode returning object
    public object DecodeValue(RegistryFriendlyByteBuf buf)
        => StreamCodec.Decode(buf);

    public override string ToString() => $"DataComponentType[{Codec?.GetType().Name ?? "transient"}]";
}
