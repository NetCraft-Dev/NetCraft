using NetCraft.Codec;

namespace NetCraft.Network;

//Filterable is a filterable text container, maps to vanilla net.minecraft.server.network.Filterable
//raw is the original text and filtered is the server-filtered version; when filtering is off only raw exists
public sealed record Filterable<T>(T Raw, Optional<T> Filtered)
{
    //CodecOf is the full form with raw plus the filtered field, degrading to a bare value when absent, maps to vanilla codec
    //The method name uses the Of suffix to avoid clashing with the Codec type
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

    //StreamCodecOf is a bare value plus an optional filtered value, maps to vanilla streamCodec
    public static StreamCodec<RegistryFriendlyByteBuf, Filterable<T>> StreamCodecOf<T>(
        StreamCodec<RegistryFriendlyByteBuf, T> valueCodec)
        => new FilterableStreamCodec<T>(valueCodec);

    //PassThrough carries only the bare value, maps to vanilla passThrough
    public static Filterable<T> PassThrough(T value) => new(value, Optional<T>.Empty());

    //Get returns the filtered value when filtering is on, otherwise the bare value, maps to vanilla get
    public T Get(bool filterEnabled) => filterEnabled ? Filtered.OrElse(Raw) : Raw;
}

//FilterableStreamCodec is a bare value plus an optional filtered value, maps to vanilla streamCodec
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
