namespace NetCraft.DataFixer.Kinds;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer.Util;

//OptionalBox容器存放Mu标记避免OptionalBox<T>类型参数上下文
public static class OptionalBoxes
{
    //一元HKT标记
    public sealed class Mu : K1 { }
}

//Optional盒对应原版com.mojang.datafixers.kinds.OptionalBox
public sealed class OptionalBox<T> : App<OptionalBoxes.Mu, T>
{
    private readonly Optional<T> _value;

    private OptionalBox(Optional<T> value) => _value = value;

    //还原类型应用为Optional<T>
    public static Optional<A> Unbox<A>(App<OptionalBoxes.Mu, A> box) => ((OptionalBox<A>)(object)box!)._value;

    //构造OptionalBox
    public static OptionalBox<A> Create<A>(Optional<A> value) => new(value);
}

//OptionalBox作为Applicative+Traversable的实例独立放置
public sealed class OptionalBoxInstance : Applicative<OptionalBoxes.Mu, OptionalBoxInstance.Mu>, Traversable<OptionalBoxes.Mu, OptionalBoxInstance.Mu>
{
    public sealed class Mu : IApplicativeMu, ITraversableMu { }
    public static readonly OptionalBoxInstance InstanceOf = new();

    public App<OptionalBoxes.Mu, R> Map<T, R>(Func<T, R> func, App<OptionalBoxes.Mu, T> ts)
        => OptionalBox<R>.Create(OptionalBox<T>.Unbox<T>(ts).Map(func));

    public App<OptionalBoxes.Mu, A> Point<A>(A a) => OptionalBox<A>.Create(Optional<A>.Of(a));

    public Func<App<OptionalBoxes.Mu, A>, App<OptionalBoxes.Mu, R>> Lift1<A, R>(App<OptionalBoxes.Mu, Func<A, R>> function)
        => a => OptionalBox<R>.Create(OptionalBox<Func<A, R>>.Unbox<Func<A, R>>(function).FlatMap(f => OptionalBox<A>.Unbox<A>(a).Map(f)));

    public Func<App<OptionalBoxes.Mu, A>, App<OptionalBoxes.Mu, B>, App<OptionalBoxes.Mu, R>> Lift2<A, B, R>(App<OptionalBoxes.Mu, Func<A, B, R>> function)
        => (a, b) => OptionalBox<R>.Create(OptionalBox<Func<A, B, R>>.Unbox<Func<A, B, R>>(function).FlatMap(f => OptionalBox<A>.Unbox<A>(a).FlatMap(av => OptionalBox<B>.Unbox<B>(b).Map(bv => f(av, bv)))));

    public App<F, App<OptionalBoxes.Mu, B>> Traverse<F, TMu2, A, B>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> function, App<OptionalBoxes.Mu, A> input) where F : K1 where TMu2 : IApplicativeMu
    {
        var traversed = OptionalBox<A>.Unbox<A>(input).Map(function);
        if (traversed.IsPresent)
        {
            return applicative.Map(b => (App<OptionalBoxes.Mu, B>)OptionalBox<B>.Create(Optional<B>.Of(b)), traversed.Get());
        }
        return applicative.Point((App<OptionalBoxes.Mu, B>)OptionalBox<B>.Create(Optional<B>.Empty()));
    }
}
