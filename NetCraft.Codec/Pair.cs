namespace NetCraft.Codec;

//简二元组对应原版com.mojang.datafixers.util.Pair
public readonly struct Pair<TFirst, TSecond>(TFirst first, TSecond second)
{
    public TFirst First { get; } = first;
    public TSecond Second { get; } = second;

    public static Pair<TFirst, TSecond> Of(TFirst first, TSecond second) => new(first, second);

    //元组解构支持
    public void Deconstruct(out TFirst first, out TSecond second)
    {
        first = First;
        second = Second;
    }

    public override string ToString() => $"({First}, {Second})";
}
