namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;

//Grates container holding the Mu marker, avoiding generic nesting
public static class Grates
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Grate<S,T,A,B>
    public static Grate<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Grate<S, T, A, B>)(object)box!;
}

//Grate grate optic maps to vanilla com.mojang.datafixers.optics.Grate
//grate takes a function of S->A functions and returns T; based on Closed
public interface Grate<S, T, A, B> : App2<Grates.Mu<A, B>, S, T>, Optic<IClosedMu, S, T, A, B>
{
    //grate takes a (Func<S,A>)->B function and returns T
    T GrateOptic(Func<Func<S, A>, B> f);

    //eval uses Closed.closed to lift A->B to (Func<S,A>)->(Func<S,B>), then dimap composes grate
    //X is explicitly specified as Func<S,A> to match vanilla Java type inference
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IClosedMu, S, T, A, B>.Eval<P>(App<IClosedMu, P> proof)
    {
        var closed = Closed<P, IClosedMu>.Unbox(proof);
        return input => closed.Dimap<Func<Func<S, A>, A>, Func<Func<S, A>, B>, S, T>(
            closed.Closed<A, B, Func<S, A>>(input),
            s => new Func<Func<S, A>, A>(f => f(s)),
            GrateOptic
        );
    }
}

//Grate concrete implementation holding a grate delegate
internal sealed class GrateImpl<S, T, A, B> : Grate<S, T, A, B>
{
    private readonly Func<Func<Func<S, A>, B>, T> _grate;
    internal GrateImpl(Func<Func<Func<S, A>, B>, T> grate) => _grate = grate;
    public T GrateOptic(Func<Func<S, A>, B> f) => _grate(f);
}

//GrateInstance as the Closed instance, implementing dimap and closed
//Java type erasure makes Grate's A2/B2 swap with the method type parameters A/B at runtime; use object casts to align
public sealed class GrateInstance<A2, B2> : Closed<Grates.Mu<A2, B2>, IClosedMu>
{
    //dimap inversely pre-maps Grate<A,B,A2,B2> with g and forward post-maps with h, building Grate<C,D,A2,B2>
    //constructs the new Grate directly without relying on eval, avoiding recursion
    public Func<App2<Grates.Mu<A2, B2>, A, B>, App2<Grates.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input =>
        {
            var grate = Grates.Unbox<A, B, A2, B2>(input);
            D NewGrateFunc(Func<Func<C, A2>, B2> f)
                => h(grate.GrateOptic(fa => f(c => fa(g(c)))));
            var newGrate = Optics.Grate<C, D, A2, B2>(NewGrateFunc);
            return (App2<Grates.Mu<A2, B2>, C, D>)(object)newGrate;
        };
    }

    //closed lifts Grate<A,B,A2,B2> to Grate<Func<X,A>,Func<X,B>,A2,B2>
    //creates a temporary Grate using this as the Closed proof; eval lifts input, maps to vanilla Optics.grate(func).eval(this).apply(input)
    public App2<Grates.Mu<A2, B2>, Func<X, A>, Func<X, B>> Closed<A, B, X>(App2<Grates.Mu<A2, B2>, A, B> input)
    {
        var grate = Grates.Unbox<A, B, A2, B2>(input);
        Func<X, B> NewGrateFunc(Func<Func<Func<X, A>, A>, B> f1)
            => x => f1(f2 => f2(x));
        var tempGrate = Optics.Grate<Func<X, A>, Func<X, B>, A, B>(NewGrateFunc);
        var evalFunc = ((Optic<IClosedMu, Func<X, A>, Func<X, B>, A, B>)tempGrate).Eval<Grates.Mu<A2, B2>>(this);
        return evalFunc.Invoke(grate);
    }
}
