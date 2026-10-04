namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;

//Procompose容器存放Mu标记避免泛型嵌套
public static class Procomposes
{
    //Mu标记包装F+G两个profunctor类型构造器
    public sealed class Mu<F, G> : K2 where F : K2 where G : K2 { }
}

//Procompose组合两个profunctor对应原版com.mojang.datafixers.optics.Procompose
//first: A->C的延迟suppliersecond: C->B的具体值组合后表达A->B
public sealed class Procompose<F, G, A, B, C> : App2<Procomposes.Mu<F, G>, A, B> where F : K2 where G : K2
{
    private readonly Func<App2<F, A, C>> _first;
    private readonly App2<G, C, B> _second;

    public Procompose(Func<App2<F, A, C>> first, App2<G, C, B> second)
    {
        _first = first;
        _second = second;
    }

    //还原类型应用为Procompose类型擦除C用object占位
    public static Procompose<F, G, A2, B2, object> Unbox<A2, B2>(App2<Procomposes.Mu<F, G>, A2, B2> box)
        => (Procompose<F, G, A2, B2, object>)(object)box!;

    //first延迟取A->C的profunctor
    public Func<App2<F, A, C>> First() => _first;
    //second取C->B的profunctor
    public App2<G, C, B> Second() => _second;
}
