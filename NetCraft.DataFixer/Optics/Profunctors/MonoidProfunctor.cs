namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;

//MonoidProfunctor monoid profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.MonoidProfunctor
//provides zero (identity element) and plus (combine), wrapping with Procompose
public interface MonoidProfunctor<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IMonoidProfunctorMu
{
    //zero returns the zero element of the function type, wrapped with FunctionTypes.Mu
    App2<P, A, B> Zero<A, B>(App2<FunctionTypes.Mu, A, B> func);

    //plus combines two profunctors wrapped by Procompose
    App2<P, A, B> Plus<A, B>(App2<Procomposes.Mu<P, P>, A, B> input);

    //compose uses plus+Procompose to compose second then first, forming A->C
    App2<P, A, C> Compose<A, B, C>(App2<P, B, C> first, Func<App2<P, A, B>> second)
        => Plus<A, C>(new Procompose<P, P, A, C, B>(second, first));
}
