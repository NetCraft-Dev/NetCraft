namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Prism container holding the Mu marker, avoiding generic nesting
public static class Prisms
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Prism<S,T,A,B>
    public static Prism<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Prism<S, T, A, B>)(object)box!;
}

//Prism prism optic maps to vanilla com.mojang.datafixers.optics.Prism
//match tries to decompose S into Either<T,A> and build constructs T from B; based on Cocartesian
public interface Prism<S, T, A, B> : App2<Prisms.Mu<A, B>, S, T>, Optic<ICocartesianMu, S, T, A, B>
{
    //match tries to decompose S: Right<A> on success, Left<T> on failure
    Either<T, A> Match(S s);
    //build constructs T from B
    T Build(B b);

    //eval uses Cocartesian.right to extend A->B to Either<T,A>->Either<T,B>
    //then dimap decomposes with match and rebuilds with build, completing the S<->T conversion
    Func<App2<P, A, B>, App2<P, S, T>> Optic<ICocartesianMu, S, T, A, B>.Eval<P>(App<ICocartesianMu, P> proof)
    {
        var cocartesian = Cocartesian<P, ICocartesianMu>.Unbox(proof);
        return input => cocartesian.Dimap<Either<T, A>, Either<T, B>, S, T>(
            cocartesian.Right<A, B, T>(input),
            Match,
            either => either.Map(e => e, Build)
        );
    }
}

//Prism concrete implementation holding match/build delegates
internal sealed class PrismImpl<S, T, A, B> : Prism<S, T, A, B>
{
    private readonly Func<S, Either<T, A>> _match;
    private readonly Func<B, T> _build;
    internal PrismImpl(Func<S, Either<T, A>> match, Func<B, T> build)
    {
        _match = match;
        _build = build;
    }
    public Either<T, A> Match(S s) => _match(s);
    public T Build(B b) => _build(b);
}

//Prism as a Cocartesian instance; with A2/B2 fixed, dimap/left/right compose match/build
public sealed class PrismInstance<A2, B2> : Cocartesian<Prisms.Mu<A2, B2>, ICocartesianMu>
{
    //dimap preprocesses the input with g and postprocesses the output with h, composing the original Prism
    public Func<App2<Prisms.Mu<A2, B2>, A, B>, App2<Prisms.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return prismBox => Optics.Prism<C, D, A2, B2>(
            c => Prisms.Unbox<A, B, A2, B2>(prismBox).Match(g(c)).MapLeft(h),
            b2 => h(Prisms.Unbox<A, B, A2, B2>(prismBox).Build(b2))
        );
    }

    //left extends Prism to the either left branch, preserving the right
    //Map explicitly specifies R2 because C# cannot infer the nested Either type
    public App2<Prisms.Mu<A2, B2>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<Prisms.Mu<A2, B2>, A, B> input)
    {
        var prism = Prisms.Unbox<A, B, A2, B2>(input);
        return Optics.Prism<Either<A, C>, Either<B, C>, A2, B2>(
            either => either.Map<Either<Either<B, C>, A2>>(
                a => prism.Match(a).MapLeft(b => Either<B, C>.Left(b)),
                c => Either<Either<B, C>, A2>.Left(Either<B, C>.Right(c))
            ),
            b2 => Either<B, C>.Left(prism.Build(b2))
        );
    }

    //right extends Prism to the either right branch, preserving the left
    public new App2<Prisms.Mu<A2, B2>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<Prisms.Mu<A2, B2>, A, B> input)
    {
        var prism = Prisms.Unbox<A, B, A2, B2>(input);
        return Optics.Prism<Either<C, A>, Either<C, B>, A2, B2>(
            either => either.Map<Either<Either<C, B>, A2>>(
                c => Either<Either<C, B>, A2>.Left(Either<C, B>.Left(c)),
                a => prism.Match(a).MapLeft(b => Either<C, B>.Right(b))
            ),
            b2 => Either<C, B>.Right(prism.Build(b2))
        );
    }
}
