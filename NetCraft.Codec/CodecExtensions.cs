namespace NetCraft.Codec;

//Codec扩展方法对应原版Codec.fieldOf/optionalFieldOf
public static class CodecExtensions
{
    //对应原版Codec.fieldOf(name)返回MapCodec<T>必填字段
    public static MapCodec<T> FieldOf<T>(this Codec<T> codec, string name)
        => new FieldMapCodec<T>(name, codec);

    //对应原版Codec.optionalFieldOf(name, default)带默认值
    public static MapCodec<T> OptionalFieldOf<T>(this Codec<T> codec, string name, T defaultValue)
        => new OptionalFieldMapCodec<T>(name, codec, defaultValue);

    //对应原版Codec.optionalFieldOf(name)返回MapCodec<Optional<T>>
    public static MapCodec<Optional<T>> OptionalFieldOf<T>(this Codec<T> codec, string name)
        => new OptionalFieldMapCodecOptional<T>(name, codec);
}
