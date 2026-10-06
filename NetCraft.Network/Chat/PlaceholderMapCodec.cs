namespace NetCraft.Network.Chat;

using NetCraft.Codec;

//Placeholder MapCodec used while ComponentContents.codec is not fully implemented
//Returns a failed DataResult to avoid null references; replaced by the real Codec later in ComponentSerialization
//Non-generic design avoids C# generic invariance preventing PlaceholderMapCodec<T> from converting to MapCodec<ComponentContents>
internal sealed class PlaceholderMapCodec : MapCodec<ComponentContents>
{
    public static readonly PlaceholderMapCodec Instance = new();

    private PlaceholderMapCodec() { }

    public DataResult<ComponentContents> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<ComponentContents>.Error(() => "codec not implemented");

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, ComponentContents value)
        => DataResult<U>.Error(() => "codec not implemented");

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, ComponentContents value, RecordBuilder<U> builder) => builder;

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => ops.MapBuilder();
}
