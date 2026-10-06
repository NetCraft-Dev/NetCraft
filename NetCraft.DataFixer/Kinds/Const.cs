namespace NetCraft.DataFixer.Kinds;

using System;

//Const container holding the Mu marker and factory methods, avoiding the Const<C,T> type parameter context
public static class Consts
{
    //unary HKT marker; C is the constant type
    public sealed class Mu<C> : K1 { }

    //build a constant container; T is the carrier type and no T value is held
    public static Const<C, T> Create<C, T>(C value) => new(value);

    //recover the type application and take the constant value
    public static C Unbox<C, T>(App<Mu<C>, T> box) => ((Const<C, T>)(object)box!).Value;
}

//constant functor maps to vanilla com.mojang.datafixers.kinds.Const
public sealed class Const<C, T> : App<Consts.Mu<C>, T>
{
    internal C Value { get; }

    internal Const(C value) => Value = value;
}

//Const as an Applicative instance; C is fixed while T is arbitrary
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
