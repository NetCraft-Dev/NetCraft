namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;

//Wander遍历策略对应原版com.mojang.datafixers.optics.Wander
//把A->B的Applicative函数扩展为S->T的Applicative函数Traversal用此抽象遍历任意容器
public interface Wander<S, T, A, B>
{
    //wander接收Applicative与A->App<F,B>函数返回S->App<F,T>函数
    //TMu2显式声明Applicative标记因为C#无Java通配符
    Func<S, App<F, T>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> input) where F : K1 where TMu2 : IApplicativeMu;
}
