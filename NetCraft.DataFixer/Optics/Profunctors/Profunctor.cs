namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//Profunctor binary typeclass maps to vanilla com.mojang.datafixers.optics.profunctors.Profunctor
//provides dimap (two-sided mapping) and lmap/rmap (one-sided mapping)
public interface Profunctor<P, TMu> : Kind2<P, TMu> where P : K2 where TMu : IProfunctorMu
{
    //marker IProfunctorMu chain-inherits IKind2Mu and K1
    interface Mu : IProfunctorMu { }

    //recover the type application as Profunctor
    //proofBox is actually a concrete instance such as FunctionTypeInstance implementing the App<TMu,P> interface
    //under C# strict generic invariance, App<FunctionTypeInstance.Mu,P> and App<IProfunctorMu,P> are different closed types and the cast fails
    //under Java type erasure App<Proof,P> equals App<Object,Object> at runtime, so any App instance can be cast
    //use Unsafe.As to bypass the runtime type check, aligning with Java virtual dispatch semantics
    static Profunctor<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : IProfunctorMu
    {
        object box = proofBox;
        return System.Runtime.CompilerServices.Unsafe.As<object, Profunctor<P2, TMu2>>(ref box!);
    }

    //dimap returns a function App2<P,A,B>->App2<P,C,D>, inversely mapping the input with g and forward-mapping the output with h
    Func<App2<P, A, B>, App2<P, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h);

    //dimap applies the function directly to the argument
    App2<P, C, D> Dimap<A, B, C, D>(App2<P, A, B> arg, Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h).Invoke(arg);

    //lmap maps only the left input, equivalent to dimap(g,identity)
    App2<P, C, B> Lmap<A, B, C>(App2<P, A, B> input, Func<C, A> g)
        => Dimap<A, B, C, B>(input, g, x => x);

    //rmap maps only the right output, equivalent to dimap(identity,h)
    App2<P, A, D> Rmap<A, B, D>(App2<P, A, B> input, Func<B, D> h)
        => Dimap<A, B, A, D>(input, x => x, h);
}
