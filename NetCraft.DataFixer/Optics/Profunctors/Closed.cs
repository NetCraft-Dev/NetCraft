namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//Closed closed profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.Closed
//provides closed, the operation on function spaces; Grate is based on this
public interface Closed<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IClosedMu
{
    //recover the type application as Closed
    static Closed<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : IClosedMu
        => (Closed<P2, TMu2>)(object)proofBox;

    //closed lifts A->B to the function-space mapping (X->A)->(X->B)
    App2<P, Func<X, A>, Func<X, B>> Closed<A, B, X>(App2<P, A, B> input);
}
