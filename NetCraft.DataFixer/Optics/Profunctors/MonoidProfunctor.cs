namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;

//MonoidProfunctor幺半profunctor对应原版com.mojang.datafixers.optics.profunctors.MonoidProfunctor
//提供zero单位元和plus合并用Procompose包装
public interface MonoidProfunctor<P, TMu> : Profunctor<P, TMu> where P : K2 where TMu : IMonoidProfunctorMu
{
    //zero返回函数类型的零元用FunctionTypes.Mu包装
    App2<P, A, B> Zero<A, B>(App2<FunctionTypes.Mu, A, B> func);

    //plus合并Procompose包装的两个profunctor
    App2<P, A, B> Plus<A, B>(App2<Procomposes.Mu<P, P>, A, B> input);

    //compose用plus+Procompose组合second后接first形成A->C
    App2<P, A, C> Compose<A, B, C>(App2<P, B, C> first, Func<App2<P, A, B>> second)
        => Plus<A, C>(new Procompose<P, P, A, C, B>(second, first));
}
