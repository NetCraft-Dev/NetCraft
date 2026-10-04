namespace NetCraft.DataFixer.Kinds;

using System;

//IdF容器存放Mu标记与工厂方法避免IdF<T>类型参数上下文
public static class IdFs
{
    //一元HKT标记
    public sealed class Mu : K1 { }

    //构造IdF<T>
    public static IdF<T> Create<T>(T value) => new(value);

    //还原类型应用取值
    //box实际可能是IdF<FR>而非IdF<T> FR是T的具体子类型(T=object FR=Dynamic<object>)
    //C#严格泛型不变性禁止(IdF<T>)(object)idf强转用Unsafe.As绕过对齐Java类型擦除语义
    public static T Get<T>(App<Mu, T> box)
    {
        var obj = (object)box!;
        var idf = System.Runtime.CompilerServices.Unsafe.As<object, IdF<T>>(ref obj);
        return idf.Value;
    }
}

//身份函子对应原版com.mojang.datafixers.kinds.IdF
public sealed class IdF<T> : App<IdFs.Mu, T>
{
    public T Value { get; }

    internal IdF(T value) => Value = value;
}

//IdF作为Functor+Applicative的实例独立放置避免IdF<T>类型参数上下文
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
