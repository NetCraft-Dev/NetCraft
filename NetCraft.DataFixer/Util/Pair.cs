namespace NetCraft.DataFixer.Util;

using System;
using NetCraft.DataFixer.Kinds;

//Pair容器存放Mu标记避免Pair<F,S>类型参数上下文
public static class Pairs
{
    //一元HKT标记S为第二类型
    public sealed class Mu<S> : K1 { }
}

//二元组对应原版com.mojang.datafixers.util.Pair的HKT版本
public sealed class Pair<F, S> : App<Pairs.Mu<S>, F>
{
    public F First { get; }
    public S Second { get; }

    public Pair(F first, S second)
    {
        First = first;
        Second = second;
    }

    //还原类型应用为Pair<F,S>
    public static Pair<F, S> Unbox(App<Pairs.Mu<S>, F> box) => (Pair<F, S>)(object)box!;

    //swap互换两值
    public Pair<S, F> Swap() => new(Second, First);

    //mapFirst仅映射第一分量
    public Pair<F2, S> MapFirst<F2>(Func<F, F2> function) => new(function(First), Second);

    //mapSecond仅映射第二分量
    public Pair<F, S2> MapSecond<S2>(Func<S, S2> function) => new(First, function(Second));

    //工厂方法
    public static Pair<F, S> Of(F first, S second) => new(first, second);

    public override string ToString() => $"({First}, {Second})";

    public override bool Equals(object? obj)
    {
        if (obj is not Pair<F, S> other) return false;
        return Equals(First, other.First) && Equals(Second, other.Second);
    }

    public override int GetHashCode() => (First?.GetHashCode() ?? 0, Second?.GetHashCode() ?? 0).GetHashCode();
}

//Pair作为Traversable+CartesianLike的实例S2为第二类型
public sealed class PairInstance<S2> : Traversable<Pairs.Mu<S2>, PairInstance<S2>.Mu>, CartesianLike<Pairs.Mu<S2>, S2, PairInstance<S2>.Mu>
{
    public sealed class Mu : ITraversableMu, ICartesianLikeMu { }

    public App<Pairs.Mu<S2>, R> Map<T, R>(Func<T, R> func, App<Pairs.Mu<S2>, T> ts)
        => Pair<T, S2>.Unbox(ts).MapFirst(func);

    public App<F, App<Pairs.Mu<S2>, B>> Traverse<F, TMu2, A, B>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> function, App<Pairs.Mu<S2>, A> input) where F : K1 where TMu2 : IApplicativeMu
    {
        var pair = Pair<A, S2>.Unbox(input);
        Func<B, App<Pairs.Mu<S2>, B>> func = b => Pair<B, S2>.Of(b, pair.Second);
        return applicative.Ap(func, function(pair.First));
    }

    public App<Pairs.Mu<S2>, A> To<A>(App<Pairs.Mu<S2>, A> input) => input;
    public App<Pairs.Mu<S2>, A> From<A>(App<Pairs.Mu<S2>, A> input) => input;
}
