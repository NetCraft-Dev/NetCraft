namespace NetCraft.DataFixer.Kinds;

using System;
using System.Collections.Generic;

//ListBox container holding the Mu marker, avoiding the ListBox<T> type parameter context
public static class ListBoxes
{
    //unary HKT marker
    public sealed class Mu : K1 { }
}

//List box maps to vanilla com.mojang.datafixers.kinds.ListBox
public sealed class ListBox<T> : App<ListBoxes.Mu, T>
{
    private readonly List<T> _value;

    private ListBox(List<T> value) => _value = value;

    //recover the type application as List<T>
    public static List<A> Unbox<A>(App<ListBoxes.Mu, A> box) => ((ListBox<A>)(object)box!)._value;

    //build a ListBox
    public static ListBox<A> Create<A>(List<A> value) => new(value);
}

//ListBox as a Traversable instance, placed separately
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

    //static traverse delegating to Instance, returning App<F,List<B>>
    public static App<F, List<B>> Traverse<F, TMu2, A, B>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> function, List<A> input) where F : K1 where TMu2 : IApplicativeMu
        => applicative.Map(ListBox<B>.Unbox<B>, InstanceOf.Traverse<F, TMu2, A, B>(applicative, function, ListBox<A>.Create(input)));

    //static flip delegating to static Traverse, matching the interface's default Flip implementation
    public static App<F, List<A>> Flip<F, TMu2, A>(Applicative<F, TMu2> applicative, List<App<F, A>> input) where F : K1 where TMu2 : IApplicativeMu
        => Traverse<F, TMu2, App<F, A>, A>(applicative, x => x, input);
}
