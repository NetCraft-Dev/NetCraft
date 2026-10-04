namespace NetCraft.Network.Chat;

using NetCraft.Codec;

//占位MapCodec对应ComponentContents.codec未完整实现时使用
//返回失败的DataResult避免null引用后续ComponentSerialization阶段替换为真实Codec
//非泛型设计避免C#泛型不变导致PlaceholderMapCodec<T>无法转MapCodec<ComponentContents>
internal sealed class PlaceholderMapCodec : MapCodec<ComponentContents>
{
    public static readonly PlaceholderMapCodec Instance = new();

    private PlaceholderMapCodec() { }

    public DataResult<ComponentContents> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<ComponentContents>.Error(() => "codec未实现");

    public DataResult<U> EncodeStart<U>(DynamicOps<U> ops, ComponentContents value)
        => DataResult<U>.Error(() => "codec未实现");

    public RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, ComponentContents value, RecordBuilder<U> builder) => builder;

    public RecordBuilder<U> Encoder<U>(DynamicOps<U> ops) => ops.MapBuilder();
}
