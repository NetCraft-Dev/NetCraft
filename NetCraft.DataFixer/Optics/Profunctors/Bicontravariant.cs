namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//Bicontravariant双反变profunctor对应原版com.mojang.datafixers.optics.profunctors.Bicontravariant
//提供cimap双侧反变映射GetterP基于此
public interface Bicontravariant<P, TMu> : Kind2<P, TMu> where P : K2 where TMu : IBicontravariantMu
{
    //cimap返回接受Supplier<App2<P,A,B>>的函数用g/h双侧反变映射到C/D
    Func<Func<App2<P, A, B>>, App2<P, C, D>> Cimap<A, B, C, D>(Func<C, A> g, Func<D, B> h);

    //cimap直接应用函数到参数
    App2<P, C, D> Cimap<A, B, C, D>(Func<App2<P, A, B>> arg, Func<C, A> g, Func<D, B> h)
        => Cimap<A, B, C, D>(g, h).Invoke(arg);
}
