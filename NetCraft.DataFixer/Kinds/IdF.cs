namespace NetCraft.DataFixer.Kinds;

using System;

//IdF container holding the Mu marker and factory methods, avoiding the IdF<T> type parameter context
public static class IdFs
{
    //unary HKT marker
    public sealed class Mu : K1 { }

    //build IdF<T>
    public static IdF<T> Create<T>(T value) => new(value);

    //recover the type application and take the value
    //box may actually be IdF<FR> rather than IdF<T>; FR is a concrete subtype of T (T=object, FR=Dynamic<object>)
    //C# strict generic invariance forbids the (IdF<T>)(object)idf cast; use Unsafe.As to bypass it and align with Java type erasure semantics
    public static T Get<T>(App<Mu, T> box)
    {
        var obj = (object)box!;
        var idf = System.Runtime.CompilerServices.Unsafe.As<object, IdF<T>>(ref obj);
        return idf.Value;
    }
}

//identity functor maps to vanilla com.mojang.datafixers.kinds.IdF
public sealed class IdF<T> : App<IdFs.Mu, T>
{
    public T Value { get; }

    internal IdF(T value) => Value = value;
}

//IdF as a Functor+Applicative instance, placed separately to avoid the IdF<T> type parameter context
public sealed class IdFInstance : Functor<IdFs.Mu, IdFInstance.Mu>, Applicative<IdFs.Mu, IdFInstance.Mu>
{
    public sealed class Mu : IApplicativeMu { }
    public static readonly IdFInstance InstanceOf = new();

    public App<IdFs.Mu, R> Map<T, R>(Func<T, R> func, App<IdFs.Mu, T> ts)
        => IdFs.Create(func(IdFs.Get(ts)));

    public App<IdFs.Mu, A> Point<A>(A a) => IdFs.Create(a);

    public Func<App<IdFs.Mu, A>, App<IdFs.Mu, R>> Lift1<A, R>(App<IdFs.Mu, Func<A, R>> function)
        => a => IdFs.Create(IdFs.Get(function).Invoke(IdFs.Get(a)));

    public Func<App<IdFs.Mu, A>, App<IdFs.Mu, B>, App<IdFs.Mu, R>> Lift2<A, B, R>(App<IdFs.Mu, Func<A, B, R>> function)
        => (a, b) => IdFs.Create(IdFs.Get(function).Invoke(IdFs.Get(a), IdFs.Get(b)));
}
