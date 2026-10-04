using NetCraft.Codec;

namespace NetCraft.DataFixer.Util;

//Codec.Optional扩展补充FlatMap与isPresent语义
public static class OptionalExtensions
{
    //flatMap对应Optional.flatMap
    public static Optional<R> FlatMap<T, R>(this Optional<T> opt, Func<T, Optional<R>> mapper)
        => opt.IsPresent ? mapper(opt.Get()) : Optional<R>.Empty();

    //filter对应Optional.filter
    public static Optional<T> Filter<T>(this Optional<T> opt, Func<T, bool> predicate)
        => opt.IsPresent && predicate(opt.Get()) ? opt : Optional<T>.Empty();

    //ifPresent对应Optional.ifPresent
    public static void IfPresent<T>(this Optional<T> opt, Action<T> action)
    {
        if (opt.IsPresent) action(opt.Get());
    }
}
