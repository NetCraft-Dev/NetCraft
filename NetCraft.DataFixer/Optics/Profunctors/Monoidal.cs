namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//Monoidal monoidal profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.Monoidal
//provides par for parallel composition and empty as the identity element; Traversal is based on this
public interface Monoidal<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IMonoidalMu
{
    //par combines two profunctor components in parallel into a Pair
    App2<P, Pair<A, C>, Pair<B, D>> Par<A, B, C, D>(App2<P, A, B> first, Func<App2<P, C, D>> second);

    //empty returns the Void->Void identity element
    App2<P, Unit, Unit> Empty();
}
