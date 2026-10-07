namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//ProfunctorFunctorWrappers container holding the Mu marker, avoiding generic nesting
public static class ProfunctorFunctorWrappers
{
    //Mu marker wrapping the three type constructors P+F+G
    public sealed class Mu<P, F, G> : K2 where P : K2 where F : K1 where G : K1 { }
}

//ProfunctorFunctorWrapper wraps a Profunctor+Functor combination, maps to vanilla com.mojang.datafixers.optics.profunctors.ProfunctorFunctorWrapper
//wraps App2<P,App<F,A>,App<G,B>> as App2<Mu<A,B>,A,B> so it can be treated as an ordinary profunctor
public sealed class ProfunctorFunctorWrapper<P, F, G, A, B> : App2<ProfunctorFunctorWrappers.Mu<P, F, G>, A, B>
    where P : K2 where F : K1 where G : K1
{
    private readonly App2<P, App<F, A>, App<G, B>> _value;

    public ProfunctorFunctorWrapper(App2<P, App<F, A>, App<G, B>> value) => _value = value;

    //takes the wrapped profunctor value
    public App2<P, App<F, A>, App<G, B>> Value() => _value;

    //recover the type application as ProfunctorFunctorWrapper
    public static ProfunctorFunctorWrapper<P2, F2, G2, A2, B2> Unbox<P2, F2, G2, A2, B2>(App2<ProfunctorFunctorWrappers.Mu<P2, F2, G2>, A2, B2> box)
        where P2 : K2 where F2 : K1 where G2 : K1
        => (ProfunctorFunctorWrapper<P2, F2, G2, A2, B2>)(object)box!;
}
