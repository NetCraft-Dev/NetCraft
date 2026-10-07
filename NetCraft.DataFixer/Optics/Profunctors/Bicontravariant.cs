namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//Bicontravariant profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.Bicontravariant
//provides cimap for two-sided contravariant mapping; GetterP is based on this
public interface Bicontravariant<P, TMu> : Kind2<P, TMu> where P : K2 where TMu : IBicontravariantMu
{
    //cimap returns a function taking Supplier<App2<P,A,B>>, mapping to C/D contravariantly on both sides with g/h
    Func<Func<App2<P, A, B>>, App2<P, C, D>> Cimap<A, B, C, D>(Func<C, A> g, Func<D, B> h);

    //cimap applies the function directly to the argument
    App2<P, C, D> Cimap<A, B, C, D>(Func<App2<P, A, B>> arg, Func<C, A> g, Func<D, B> h)
        => Cimap<A, B, C, D>(g, h).Invoke(arg);
}
