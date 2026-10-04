namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;

//ProfunctorFunctorWrappers容器存放Mu标记避免泛型嵌套
public static class ProfunctorFunctorWrappers
{
    //Mu标记包装P+F+G三个类型构造器
    public sealed class Mu<P, F, G> : K2 where P : K2 where F : K1 where G : K1 { }
}

//ProfunctorFunctorWrapper包装Profunctor+Functor组合对应原版com.mojang.datafixers.optics.profunctors.ProfunctorFunctorWrapper
//把App2<P,App<F,A>,App<G,B>>包装为App2<Mu<A,B>,A,B>使其可作为普通profunctor处理
public sealed class ProfunctorFunctorWrapper<P, F, G, A, B> : App2<ProfunctorFunctorWrappers.Mu<P, F, G>, A, B>
    where P : K2 where F : K1 where G : K1
{
    private readonly App2<P, App<F, A>, App<G, B>> _value;

    public ProfunctorFunctorWrapper(App2<P, App<F, A>, App<G, B>> value) => _value = value;

    //取被包装的profunctor值
    public App2<P, App<F, A>, App<G, B>> Value() => _value;

    //还原类型应用为ProfunctorFunctorWrapper
    public static ProfunctorFunctorWrapper<P2, F2, G2, A2, B2> Unbox<P2, F2, G2, A2, B2>(App2<ProfunctorFunctorWrappers.Mu<P2, F2, G2>, A2, B2> box)
        where P2 : K2 where F2 : K1 where G2 : K1
        => (ProfunctorFunctorWrapper<P2, F2, G2, A2, B2>)(object)box!;
}
