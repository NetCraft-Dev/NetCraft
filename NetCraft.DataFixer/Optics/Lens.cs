namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Lens container holding the Mu marker, avoiding generic nesting
public static class Lenses
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Lens<S,T,A,B>
    public static Lens<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Lens<S, T, A, B>)(object)box!;
}

//Lens lens optic maps to vanilla com.mojang.datafixers.optics.Lens
//view takes the focus and update replaces it; based on Cartesian
public interface Lens<S, T, A, B> : App2<Lenses.Mu<A, B>, S, T>, Optic<ICartesianMu, S, T, A, B>
{
    //view takes the focus A from S
    A View(S s);
    //update replaces the focus with B to get T, preserving the rest of S
    T Update(B b, S s);

    //eval uses Cartesian.first to extend A->B to Pair<A,S>->Pair<B,S>
    //then dimap converts Pair<A,S>->Pair<B,S> into S->T
    //Dimap type parameters are explicit to avoid C# lambda inference failure
    Func<App2<P, A, B>, App2<P, S, T>> Optic<ICartesianMu, S, T, A, B>.Eval<P>(App<ICartesianMu, P> proof)
    {
        var cartesian = Cartesian<P, ICartesianMu>.Unbox(proof);
        return a => cartesian.Dimap<Pair<A, S>, Pair<B, S>, S, T>(
            cartesian.First<A, B, S>(a),
            s => Pair<A, S>.Of(View(s), s),
            pair => Update(pair.First, pair.Second)
        );
    }
}

//Lens concrete implementation holding view/update delegates
internal sealed class LensImpl<S, T, A, B> : Lens<S, T, A, B>
{
    private readonly Func<S, A> _view;
    private readonly Func<B, S, T> _update;
    internal LensImpl(Func<S, A> view, Func<B, S, T> update)
    {
        _view = view;
        _update = update;
    }
    public A View(S s) => _view(s);
    public T Update(B b, S s) => _update(b, s);
}

//Lens as a Cartesian instance; with A2/B2 fixed, dimap/first/second compose view/update
public sealed class LensInstance<A2, B2> : Cartesian<Lenses.Mu<A2, B2>, ICartesianMu>
{
    //dimap preprocesses the input with g and postprocesses the output with h, composing the original Lens
    public Func<App2<Lenses.Mu<A2, B2>, A, B>, App2<Lenses.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return l => Optics.Lens<C, D, A2, B2>(
            c => Lenses.Unbox<A, B, A2, B2>(l).View(g(c)),
            (b2, c) => h(Lenses.Unbox<A, B, A2, B2>(l).Update(b2, g(c)))
        );
    }

    //first extends Lens to the first component of Pair, preserving the second
    public App2<Lenses.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Lenses.Mu<A2, B2>, A, B> input)
        => Optics.Lens<Pair<A, C>, Pair<B, C>, A2, B2>(
            pair => Lenses.Unbox<A, B, A2, B2>(input).View(pair.First),
            (b2, pair) => Pair<B, C>.Of(Lenses.Unbox<A, B, A2, B2>(input).Update(b2, pair.First), pair.Second)
        );

    //second extends Lens to the second component of Pair, preserving the first
    public new App2<Lenses.Mu<A2, B2>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<Lenses.Mu<A2, B2>, A, B> input)
        => Optics.Lens<Pair<C, A>, Pair<C, B>, A2, B2>(
            pair => Lenses.Unbox<A, B, A2, B2>(input).View(pair.Second),
            (b2, pair) => Pair<C, B>.Of(pair.First, Lenses.Unbox<A, B, A2, B2>(input).Update(b2, pair.Second))
        );
}
