namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Util;

//Monoidal幺半profunctor对应原版com.mojang.datafixers.optics.profunctors.Monoidal
//提供par并行组合与empty单位元Traversal基于此
public interface Monoidal<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IMonoidalMu
{
    //par并行组合两个profunctor分量合并到Pair
    App2<P, Pair<A, C>, Pair<B, D>> Par<A, B, C, D>(App2<P, A, B> first, Func<App2<P, C, D>> second);

    //empty返回Void->Void的单位元
    App2<P, Unit, Unit> Empty();
}
