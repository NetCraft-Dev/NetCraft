namespace NetCraft.DataFixer.Util;

using System;
using NetCraft.DataFixer.Kinds;

//Pair container holding the Mu marker, avoiding the Pair<F,S> type parameter context
public static class Pairs
{
    //unary HKT marker; S is the second type
    public sealed class Mu<S> : K1 { }
}

//pair maps to the HKT version of vanilla com.mojang.datafixers.util.Pair
public sealed class Pair<F, S> : App<Pairs.Mu<S>, F>
{
    public F First { get; }
    public S Second { get; }

    public Pair(F first, S second)
    {
        First = first;
        Second = second;
    }

    //recover the type application as Pair<F,S>
    public static Pair<F, S> Unbox(App<Pairs.Mu<S>, F> box) => (Pair<F, S>)(object)box!;

    //swap the two values
    public Pair<S, F> Swap() => new(Second, First);

    //mapFirst maps only the first component
    public Pair<F2, S> MapFirst<F2>(Func<F, F2> function) => new(function(First), Second);

    //mapSecond maps only the second component
    public Pair<F, S2> MapSecond<S2>(Func<S, S2> function) => new(First, function(Second));

    //factory method
    public static Pair<F, S> Of(F first, S second) => new(first, second);

    public override string ToString() => $"({First}, {Second})";

    public override bool Equals(object? obj)
    {
        if (obj is not Pair<F, S> other) return false;
        return Equals(First, other.First) && Equals(Second, other.Second);
    }

    public override int GetHashCode() => (First?.GetHashCode() ?? 0, Second?.GetHashCode() ?? 0).GetHashCode();
}

//Pair as a Traversable+CartesianLike instance; S2 is the second type
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
