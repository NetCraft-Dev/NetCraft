namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//Closed闭profunctor对应原版com.mojang.datafixers.optics.profunctors.Closed
//提供closed对函数空间的操作Grate基于此
public interface Closed<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IClosedMu
{
    //还原类型应用为Closed
    static Closed<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : IClosedMu
        => (Closed<P2, TMu2>)(object)proofBox;

    //closed把A->B提升为(X->A)->(X->B)的函数空间映射
    App2<P, Func<X, A>, Func<X, B>> Closed<A, B, X>(App2<P, A, B> input);
}
