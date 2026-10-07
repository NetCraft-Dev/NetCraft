namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Affine container holding the Mu marker, avoiding generic nesting
public static class Affines
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Affine<S,T,A,B>
    public static Affine<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Affine<S, T, A, B>)(object)box!;
}

//Affine affine optic maps to vanilla com.mojang.datafixers.optics.Affine
//preview tries to take the focus and set replaces it; based on AffineP=Cartesian+Cocartesian
public interface Affine<S, T, A, B> : App2<Affines.Mu<A, B>, S, T>, Optic<IAffinePMu, S, T, A, B>
{
    //preview tries to take the focus: Right<A> on success, Left<T> on failure
    Either<T, A> Preview(S s);
    //set replaces the focus with B to get T, preserving the rest of S
    T Set(B b, S s);

    //eval uses Cartesian.first to extend into a Pair, rmaps with set to replace, then Cocartesian.left to add a branch
    //finally dimap decomposes with preview and merges with Either.unwrap, completing S<->T
    //Dimap type parameters are explicit to avoid C# lambda inference failure
    //left's C=T lets h call Either<T,T>.Unwrap to merge left and right
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IAffinePMu, S, T, A, B>.Eval<P>(App<IAffinePMu, P> proof)
    {
        var cartesian = Cartesian<P, IAffinePMu>.Unbox(proof);
        var cocartesian = Cocartesian<P, IAffinePMu>.Unbox(proof);
        return input => cartesian.Dimap<Either<Pair<A, S>, T>, Either<T, T>, S, T>(
            cocartesian.Left<Pair<A, S>, T, T>(
                cartesian.Rmap<Pair<A, S>, Pair<B, S>, T>(
                    cartesian.First<A, B, S>(input),
                    p => Set(p.First, p.Second)
                )
            ),
            s => Preview(s).Map(
                t => Either<Pair<A, S>, T>.Right(t),
                a => Either<Pair<A, S>, T>.Left(Pair<A, S>.Of(a, s))
            ),
            Either<T, T>.Unwrap
        );
    }
}

//Affine concrete implementation holding preview/set delegates
internal sealed class AffineImpl<S, T, A, B> : Affine<S, T, A, B>
{
    private readonly Func<S, Either<T, A>> _preview;
    private readonly Func<B, S, T> _set;
    internal AffineImpl(Func<S, Either<T, A>> preview, Func<B, S, T> set)
    {
        _preview = preview;
        _set = set;
    }
    public Either<T, A> Preview(S s) => _preview(s);
    public T Set(B b, S s) => _set(b, s);
}

//Affine as an AffineP instance; with A2/B2 fixed, dimap/first/second/left/right compose preview/set
public sealed class AffineInstance<A2, B2> : AffineP<Affines.Mu<A2, B2>, IAffinePMu>
{
    //dimap preprocesses the input with g and postprocesses the output with h, composing the original Affine
    public Func<App2<Affines.Mu<A2, B2>, A, B>, App2<Affines.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return affineBox => Optics.Affine<C, D, A2, B2>(
            c => Affines.Unbox<A, B, A2, B2>(affineBox).Preview(g(c)).MapLeft(h),
            (b2, c) => h(Affines.Unbox<A, B, A2, B2>(affineBox).Set(b2, g(c)))
        );
    }

    //first extends Affine to the first component of Pair, preserving the second
    public App2<Affines.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Affines.Mu<A2, B2>, A, B> input)
    {
        var affine = Affines.Unbox<A, B, A2, B2>(input);
        return Optics.Affine<Pair<A, C>, Pair<B, C>, A2, B2>(
            pair => affine.Preview(pair.First).MapBoth(b => Pair<B, C>.Of(b, pair.Second), a => a),
            (b2, pair) => Pair<B, C>.Of(affine.Set(b2, pair.First), pair.Second)
        );
    }

    //left extends Affine to the either left branch, preserving the right
    public App2<Affines.Mu<A2, B2>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<Affines.Mu<A2, B2>, A, B> input)
    {
        var affine = Affines.Unbox<A, B, A2, B2>(input);
        return Optics.Affine<Either<A, C>, Either<B, C>, A2, B2>(
            either => either.Map(
                a => affine.Preview(a).MapLeft(b => Either<B, C>.Left(b)),
                c => Either<Either<B, C>, A2>.Left(Either<B, C>.Right(c))
            ),
            (b, either) => either.Map(
                l => Either<B, C>.Left(affine.Set(b, l)),
                c => Either<B, C>.Right(c)
            )
        );
    }
}
