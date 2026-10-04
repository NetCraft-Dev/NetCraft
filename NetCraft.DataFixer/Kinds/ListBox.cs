namespace NetCraft.DataFixer.Kinds;

using System;
using System.Collections.Generic;

//ListBox容器存放Mu标记避免ListBox<T>类型参数上下文
public static class ListBoxes
{
    //一元HKT标记
    public sealed class Mu : K1 { }
}

//List盒对应原版com.mojang.datafixers.kinds.ListBox
public sealed class ListBox<T> : App<ListBoxes.Mu, T>
{
    private readonly List<T> _value;

    private ListBox(List<T> value) => _value = value;

    //还原类型应用为List<T>
    public static List<A> Unbox<A>(App<ListBoxes.Mu, A> box) => ((ListBox<A>)(object)box!)._value;

    //构造ListBox
    public static ListBox<A> Create<A>(List<A> value) => new(value);
}

//ListBox作为Traversable的实例独立放置
public sealed class ListBoxInstance : Traversable<ListBoxes.Mu, ListBoxInstance.Mu>
{
    public sealed class Mu : ITraversableMu { }
    public static readonly ListBoxInstance InstanceOf = new();

    public App<ListBoxes.Mu, R> Map<T, R>(Func<T, R> func, App<ListBoxes.Mu, T> ts)
    {
        var list = ListBox<T>.Unbox<T>(ts);
        var result = new List<R>(list.Count);
        foreach (var item in list) result.Add(func(item));
        return ListBox<R>.Create(result);
    }

    public App<F, App<ListBoxes.Mu, B>> Traverse<F, TMu2, A, B>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> function, App<ListBoxes.Mu, A> input) where F : K1 where TMu2 : IApplicativeMu
    {
        var list = ListBox<A>.Unbox<A>(input);
        App<F, List<B>> result = applicative.Point(new List<B>());

        foreach (var a in list)
        {
            App<F, B> fb = function(a);
            result = applicative.Apply2((acc, b) =>
            {
                var next = new List<B>(acc.Count + 1);
                next.AddRange(acc);
                next.Add(b);
                return next;
            }, result, fb);
        }

        return applicative.Map(b => (App<ListBoxes.Mu, B>)ListBox<B>.Create(b), result);
    }

    //静态遍历委托Instance返回App<F,List<B>>
    public static App<F, List<B>> Traverse<F, TMu2, A, B>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> function, List<A> input) where F : K1 where TMu2 : IApplicativeMu
        => applicative.Map(ListBox<B>.Unbox<B>, InstanceOf.Traverse<F, TMu2, A, B>(applicative, function, ListBox<A>.Create(input)));

    //静态翻转委托静态Traverse对应接口Flip默认实现
    public static App<F, List<A>> Flip<F, TMu2, A>(Applicative<F, TMu2> applicative, List<App<F, A>> input) where F : K1 where TMu2 : IApplicativeMu
        => Traverse<F, TMu2, App<F, A>, A>(applicative, x => x, input);
}
