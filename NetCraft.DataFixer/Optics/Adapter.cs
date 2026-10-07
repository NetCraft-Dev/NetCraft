namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;

//Adapter container holding the Mu marker, avoiding generic nesting
public static class Adapters
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Adapter<S,T,A,B>
    public static Adapter<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Adapter<S, T, A, B>)(object)box!;
}

//Adapter adapter optic maps to vanilla com.mojang.datafixers.optics.Adapter
//the simplest optic; from/to directly convert S->A and B->T
//uses IProfunctorMu instead of vanilla Profunctor.Mu as the Proof marker
public interface Adapter<S, T, A, B> : App2<Adapters.Mu<A, B>, S, T>, Optic<IProfunctorMu, S, T, A, B>
{
    //takes the focus A from S
    A From(S s);
    //puts B back to get T
    T To(B b);

    //eval composes from/to with Profunctor.dimap
    //explicitly specifies the Dimap method type parameters to match Adapter's A/B/S/T, avoiding inference ambiguity
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IProfunctorMu, S, T, A, B>.Eval<P>(App<IProfunctorMu, P> proof)
    {
        var profunctor = Profunctor<P, IProfunctorMu>.Unbox(proof);
        return a => profunctor!.Dimap<A, B, S, T>(a, From, To);
    }
}

//Adapter concrete implementation holding from/to delegates
internal sealed class AdapterImpl<S, T, A, B> : Adapter<S, T, A, B>
{
    private readonly Func<S, A> _from;
    private readonly Func<B, T> _to;
    internal AdapterImpl(Func<S, A> from, Func<B, T> to)
    {
        _from = from;
        _to = to;
    }
    public A From(S s) => _from(s);
    public T To(B b) => _to(b);
}

//Adapter as a Profunctor instance; with A2/B2 fixed, dimap composes from/to
public sealed class AdapterInstance<A2, B2> : Profunctor<Adapters.Mu<A2, B2>, IProfunctorMu>
{
    //dimap inversely maps the input with g and forward-maps the output with h, building a new Adapter
    //Unbox specifies <A,B,A2,B2> because the Adapter wrapped by Mu<A2,B2> has focus/new value types fixed to A2/B2
    public Func<App2<Adapters.Mu<A2, B2>, A, B>, App2<Adapters.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return a => Optics.Adapter<C, D, A2, B2>(
            c => Adapters.Unbox<A, B, A2, B2>(a).From(g(c)),
            b2 => h(Adapters.Unbox<A, B, A2, B2>(a).To(b2))
        );
    }
}
