namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;

//Getters container holding the Mu marker, avoiding generic nesting
public static class Getters
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Getter<S,T,A,B>
    public static Getter<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Getter<S, T, A, B>)(object)box!;
}

//Getter getter optic maps to vanilla com.mojang.datafixers.optics.Getter
//read-only view taking the focus, based on GetterP (Profunctor+Bicontravariant)
public interface Getter<S, T, A, B> : App2<Getters.Mu<A, B>, S, T>, Optic<IGetterPMu, S, T, A, B>
{
    //get takes the focus A from S
    A Get(S s);

    //eval uses GetterP.secondPhantom to attach the phantom second component, then lmaps it with get
    //SecondPhantom returns App2<P,A,A>; the right component is phantom-cast to App2<P,S,T>
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IGetterPMu, S, T, A, B>.Eval<P>(App<IGetterPMu, P> proof)
    {
        var getterP = GetterP<P, IGetterPMu>.Unbox(proof);
        return input => (App2<P, S, T>)(object)getterP.Lmap<A, A, S>(getterP.SecondPhantom<A, B, A>(input), Get);
    }
}

//Getter concrete implementation holding a get delegate
internal sealed class GetterImpl<S, T, A, B> : Getter<S, T, A, B>
{
    private readonly Func<S, A> _get;
    internal GetterImpl(Func<S, A> get) => _get = get;
    public A Get(S s) => _get(s);
}

//GetterInstance as the GetterP instance
//dimap preprocesses the input with g and composes the original Getter.get; cimap uses a Supplier for lazy retrieval
public sealed class GetterInstance<A2, B2> : GetterP<Getters.Mu<A2, B2>, IGetterPMu>
{
    //dimap preprocesses the input with g, then calls the original Getter.get
    public Func<App2<Getters.Mu<A2, B2>, A, B>, App2<Getters.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.Getter<C, D, A2, B2>(c => Getters.Unbox<A, B, A2, B2>(input).Get(g(c)));

    //cimap lazily retrieves via Supplier, composing the original Getter.get with g
    public Func<Func<App2<Getters.Mu<A2, B2>, A, B>>, App2<Getters.Mu<A2, B2>, C, D>> Cimap<A, B, C, D>(Func<C, A> g, Func<D, B> h)
        => input => Optics.Getter<C, D, A2, B2>(c => Getters.Unbox<A, B, A2, B2>(input()).Get(g(c)));
}
