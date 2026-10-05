using NetCraft.Codec;

namespace NetCraft.Network;

//Filterable 可过筛文本容器 对应原版 net.minecraft.server.network.Filterable
//raw 是原始文本 filtered 是服务端过滤后的版本 过滤未开启时只有 raw
public sealed record Filterable<T>(T Raw, Optional<T> Filtered)
{
    //CodecOf 完整形态 raw 加 filtered 二字段 缺失退化成裸值 对应原版 codec
    //方法名带 Of 后缀避免与 Codec 类型同名
    public static Codec<Filterable<T>> CodecOf(Codec<T> valueCodec)
    {
        var fullCodec = RecordCodecBuilder.Of2(
            valueCodec.FieldOf("raw").ForGetter((Filterable<T> filterable) => filterable.Raw),
            valueCodec.OptionalFieldOf("filtered").ForGetter((Filterable<T> filterable) => filterable.Filtered),
            (raw, filtered) => new Filterable<T>(raw, filtered));
        var simpleCodec = valueCodec.ComapFlatMap(
            value => DataResult<Filterable<T>>.Success(PassThrough(value)),
            filterable => filterable.Raw);
        return Codecs.WithAlternative(fullCodec, simpleCodec);
    }

    //StreamCodecOf 裸值加可选过滤值 对应原版 streamCodec
    public static StreamCodec<RegistryFriendlyByteBuf, Filterable<T>> StreamCodecOf<T>(
        StreamCodec<RegistryFriendlyByteBuf, T> valueCodec)
        => new FilterableStreamCodec<T>(valueCodec);

    //PassThrough 只带裸值 对应原版 passThrough
    public static Filterable<T> PassThrough(T value) => new(value, Optional<T>.Empty());

    //Get 过滤开启时取过滤值 否则取裸值 对应原版 get
    public T Get(bool filterEnabled) => filterEnabled ? Filtered.OrElse(Raw) : Raw;
}

//FilterableStreamCodec 裸值加可选过滤值 对应原版 streamCodec
internal sealed class FilterableStreamCodec<T> : StreamCodec<RegistryFriendlyByteBuf, Filterable<T>>
{
    private readonly StreamCodec<RegistryFriendlyByteBuf, T> _valueCodec;

    public FilterableStreamCodec(StreamCodec<RegistryFriendlyByteBuf, T> valueCodec) => _valueCodec = valueCodec;

    public Filterable<T> Decode(RegistryFriendlyByteBuf buf)
    {
        var raw = _valueCodec.Decode(buf);
        var filtered = buf.ReadBoolean() ? Optional<T>.Of(_valueCodec.Decode(buf)) : Optional<T>.Empty();
        return new Filterable<T>(raw, filtered);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Filterable<T> value)
    {
        _valueCodec.Encode(buf, value.Raw);
        buf.WriteBoolean(value.Filtered.IsPresent);
        if (value.Filtered.IsPresent) _valueCodec.Encode(buf, value.Filtered.Get());
    }
}
