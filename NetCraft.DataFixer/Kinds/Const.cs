namespace NetCraft.DataFixer.Kinds;

using System;

//Const容器存放Mu标记与工厂方法避免Const<C,T>类型参数上下文
public static class Consts
{
    //一元HKT标记C为常量类型
    public sealed class Mu<C> : K1 { }

    //构造常量容器T为载体类型不持有T值
    public static Const<C, T> Create<C, T>(C value) => new(value);

    //还原类型应用取常量值
    public static C Unbox<C, T>(App<Mu<C>, T> box) => ((Const<C, T>)(object)box!).Value;
}

//常量函子对应原版com.mojang.datafixers.kinds.Const
public sealed class Const<C, T> : App<Consts.Mu<C>, T>
{
    internal C Value { get; }

    internal Const(C value) => Value = value;
}

//Const作为Applicative的实例C固定后T任意
public sealed class ConstInstance<C> : Applicative<Consts.Mu<C>, ConstInstance<C>.Mu>
{
    public sealed class Mu : IApplicativeMu { }

    private readonly Monoid<C> _monoid;
    public ConstInstance(Monoid<C> monoid) => _monoid = monoid;

    public App<Consts.Mu<C>, R> Map<T, R>(Func<T, R> func, App<Consts.Mu<C>, T> ts)
        => Consts.Create<C, R>(Consts.Unbox<C, T>(ts));

    public App<Consts.Mu<C>, A> Point<A>(A a) => Consts.Create<C, A>(_monoid.Point());

    public Func<App<Consts.Mu<C>, A>, App<Consts.Mu<C>, R>> Lift1<A, R>(App<Consts.Mu<C>, Func<A, R>> function)
        => a => Consts.Create<C, R>(_monoid.Add(Consts.Unbox<C, Func<A, R>>(function), Consts.Unbox<C, A>(a)));

    public Func<App<Consts.Mu<C>, A>, App<Consts.Mu<C>, B>, App<Consts.Mu<C>, R>> Lift2<A, B, R>(App<Consts.Mu<C>, Func<A, B, R>> function)
        => (a, b) => Consts.Create<C, R>(_monoid.Add(Consts.Unbox<C, Func<A, B, R>>(function), _monoid.Add(Consts.Unbox<C, A>(a), Consts.Unbox<C, B>(b))));
}
