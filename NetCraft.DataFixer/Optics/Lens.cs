namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Lens容器存放Mu标记避免泛型嵌套
public static class Lenses
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Lens<S,T,A,B>
    public static Lens<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Lens<S, T, A, B>)(object)box!;
}

//Lens透镜光学对应原版com.mojang.datafixers.optics.Lens
//view取焦点update替换焦点基于Cartesian
public interface Lens<S, T, A, B> : App2<Lenses.Mu<A, B>, S, T>, Optic<ICartesianMu, S, T, A, B>
{
    //view从S取焦点A
    A View(S s);
    //update用B替换焦点得T保留S其他部分
    T Update(B b, S s);

    //eval用Cartesian.first把A->B扩展为Pair<A,S>->Pair<B,S>
    //再dimap把Pair<A,S>->Pair<B,S>转换为S->T
    //Dimap类型参数显式指定避免C#lambda推断失败
    Func<App2<P, A, B>, App2<P, S, T>> Optic<ICartesianMu, S, T, A, B>.Eval<P>(App<ICartesianMu, P> proof)
    {
        var cartesian = Cartesian<P, ICartesianMu>.Unbox(proof);
        return a => cartesian.Dimap<Pair<A, S>, Pair<B, S>, S, T>(
            cartesian.First<A, B, S>(a),
            s => Pair<A, S>.Of(View(s), s),
            pair => Update(pair.First, pair.Second)
        );
    }
}

//Lens具体实现持有view/update委托
internal sealed class LensImpl<S, T, A, B> : Lens<S, T, A, B>
{
    private readonly Func<S, A> _view;
    private readonly Func<B, S, T> _update;
    internal LensImpl(Func<S, A> view, Func<B, S, T> update)
    {
        _view = view;
        _update = update;
    }
    public A View(S s) => _view(s);
    public T Update(B b, S s) => _update(b, s);
}

//Lens作为Cartesian实例A2/B2固定后dimap/first/second组合view/update
public sealed class LensInstance<A2, B2> : Cartesian<Lenses.Mu<A2, B2>, ICartesianMu>
{
    //dimap用g前处理输入h后处理输出组合原Lens
    public Func<App2<Lenses.Mu<A2, B2>, A, B>, App2<Lenses.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return l => Optics.Lens<C, D, A2, B2>(
            c => Lenses.Unbox<A, B, A2, B2>(l).View(g(c)),
            (b2, c) => h(Lenses.Unbox<A, B, A2, B2>(l).Update(b2, g(c)))
        );
    }

    //first把Lens扩展到Pair第一分量保留第二分量
    public App2<Lenses.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Lenses.Mu<A2, B2>, A, B> input)
        => Optics.Lens<Pair<A, C>, Pair<B, C>, A2, B2>(
            pair => Lenses.Unbox<A, B, A2, B2>(input).View(pair.First),
            (b2, pair) => Pair<B, C>.Of(Lenses.Unbox<A, B, A2, B2>(input).Update(b2, pair.First), pair.Second)
        );

    //second把Lens扩展到Pair第二分量保留第一分量
    public new App2<Lenses.Mu<A2, B2>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<Lenses.Mu<A2, B2>, A, B> input)
        => Optics.Lens<Pair<C, A>, Pair<C, B>, A2, B2>(
            pair => Lenses.Unbox<A, B, A2, B2>(input).View(pair.Second),
            (b2, pair) => Pair<C, B>.Of(pair.First, Lenses.Unbox<A, B, A2, B2>(input).Update(b2, pair.Second))
        );
}
