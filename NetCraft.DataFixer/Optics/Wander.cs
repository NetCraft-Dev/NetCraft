namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;

//Wander traversal strategy maps to vanilla com.mojang.datafixers.optics.Wander
//lifts an A->B Applicative function to an S->T Applicative function; Traversal uses this abstraction to traverse any container
public interface Wander<S, T, A, B>
{
    //wander takes an Applicative and an A->App<F,B> function, returning an S->App<F,T> function
    //TMu2 explicitly declares the Applicative marker because C# has no Java wildcards
    Func<S, App<F, T>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> input) where F : K1 where TMu2 : IApplicativeMu;
}
