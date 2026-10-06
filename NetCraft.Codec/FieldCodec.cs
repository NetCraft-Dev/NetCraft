namespace NetCraft.Codec;

//Field definition, mirroring vanilla RecordCodecBuilder.unpaired
//Wraps a MapCodec<F> together with a getter for record encoding and decoding
public sealed class FieldCodec<T, F>
{
    public MapCodec<F> Codec { get; }
    public Func<T, F> Getter { get; }

    public FieldCodec(MapCodec<F> codec, Func<T, F> getter)
    {
        Codec = codec;
        Getter = getter;
    }

    public static FieldCodec<T, F> Of(MapCodec<F> codec, Func<T, F> getter)
        => new(codec, getter);
}

//MapCodec<F> extension method, mirroring vanilla forGetter
//Combines a MapCodec<F> with a getter into a FieldCodec<T,F>
public static class FieldCodecExtensions
{
    //Mirrors vanilla forGetter combining a MapCodec with a getter
    public static FieldCodec<T, F> ForGetter<T, F>(this MapCodec<F> codec, Func<T, F> getter)
        => new(codec, getter);
}
