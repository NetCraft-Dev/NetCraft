namespace NetCraft.Codec;

//字段定义对应原版RecordCodecBuilder.unpaired
//包装MapCodec<F>和getter用于record编解码
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

//MapCodec<F>扩展方法对应原版forGetter
//把MapCodec<F>与getter组合成FieldCodec<T,F>
public static class FieldCodecExtensions
{
    //对应原版forGetter组合MapCodec与getter
    public static FieldCodec<T, F> ForGetter<T, F>(this MapCodec<F> codec, Func<T, F> getter)
        => new(codec, getter);
}
