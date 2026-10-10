namespace NetCraft.Codec;

//Codec extension methods, mirroring vanilla Codec.fieldOf/optionalFieldOf
public static class CodecExtensions
{
    //Mirrors vanilla Codec.fieldOf(name), returning a MapCodec<T> for a required field
    public static MapCodec<T> FieldOf<T>(this Codec<T> codec, string name)
        => new FieldMapCodec<T>(name, codec);

    //Mirrors vanilla Codec.optionalFieldOf(name, default) with a default value
    public static MapCodec<T> OptionalFieldOf<T>(this Codec<T> codec, string name, T defaultValue)
        => new OptionalFieldMapCodec<T>(name, codec, defaultValue);

    //Mirrors vanilla Codec.optionalFieldOf(name), returning a MapCodec<Optional<T>>
    public static MapCodec<Optional<T>> OptionalFieldOf<T>(this Codec<T> codec, string name)
        => new OptionalFieldMapCodecOptional<T>(name, codec);

    //Mirrors vanilla MapCodec.flatXmap, remapping the value with a pair of fallible functions
    public static MapCodec<R> FlatXmap<T, R>(this MapCodec<T> codec, Func<T, DataResult<R>> to, Func<R, DataResult<T>> from)
        => new FlatXmapMapCodec<T, R>(codec, to, from);

    //Mirrors vanilla MapCodec.xmap, remapping the value with a pair of total functions
    public static MapCodec<R> Xmap<T, R>(this MapCodec<T> codec, Func<T, R> to, Func<R, T> from)
        => new FlatXmapMapCodec<T, R>(codec, t => DataResult<R>.Success(to(t)), r => DataResult<T>.Success(from(r)));

    //Mirrors vanilla Codec.xmap, remapping with a pair of total functions while staying a Codec
    public static Codec<R> Xmap<T, R>(this Codec<T> codec, Func<T, R> to, Func<R, T> from)
        => codec.ComapFlatMap(t => DataResult<R>.Success(to(t)), from);

    //Mirrors vanilla Codec.validate, rejecting or rewriting values that fail the checker
    public static Codec<T> Validate<T>(this Codec<T> codec, Func<T, DataResult<T>> checker)
        => new ValidateCodec<T>(codec, checker);
}
