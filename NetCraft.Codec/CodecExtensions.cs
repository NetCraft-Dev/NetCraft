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
}
