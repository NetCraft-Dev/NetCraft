namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;

//Grates容器存放Mu标记避免泛型嵌套
public static class Grates
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Grate<S,T,A,B>
    public static Grate<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Grate<S, T, A, B>)(object)box!;
}

//Grate格栅光学对应原版com.mojang.datafixers.optics.Grate
//grate接收S->A的函数的函数返回T基于Closed
public interface Grate<S, T, A, B> : App2<Grates.Mu<A, B>, S, T>, Optic<IClosedMu, S, T, A, B>
{
    //grate接收(Func<S,A>)->B的函数返回T
    T GrateOptic(Func<Func<S, A>, B> f);

    //eval用Closed.closed把A->B提升为(Func<S,A>)->(Func<S,B>)再dimap组合grate
    //X显式指定为Func<S,A>对齐原版Java类型推断
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IClosedMu, S, T, A, B>.Eval<P>(App<IClosedMu, P> proof)
    {
        var closed = Closed<P, IClosedMu>.Unbox(proof);
        return input => closed.Dimap<Func<Func<S, A>, A>, Func<Func<S, A>, B>, S, T>(
            closed.Closed<A, B, Func<S, A>>(input),
            s => new Func<Func<S, A>, A>(f => f(s)),
            GrateOptic
        );
    }
}

//Grate具体实现持有grate委托
internal sealed class GrateImpl<S, T, A, B> : Grate<S, T, A, B>
{
    private readonly Func<Func<Func<S, A>, B>, T> _grate;
    internal GrateImpl(Func<Func<Func<S, A>, B>, T> grate) => _grate = grate;
    public T GrateOptic(Func<Func<S, A>, B> f) => _grate(f);
}

//GrateInstance作为Closed实例实现dimap与closed
//Java类型擦除让Grate的A2/B2与方法类型参数A/B运行时互换用object强转对齐
public sealed class GrateInstance<A2, B2> : Closed<Grates.Mu<A2, B2>, IClosedMu>
{
    //dimap把Grate<A,B,A2,B2>逆前映射g正后映射h构造Grate<C,D,A2,B2>
    //直接构造新Grate不依赖eval避免递归
    public Func<App2<Grates.Mu<A2, B2>, A, B>, App2<Grates.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input =>
        {
            var grate = Grates.Unbox<A, B, A2, B2>(input);
            D NewGrateFunc(Func<Func<C, A2>, B2> f)
                => h(grate.GrateOptic(fa => f(c => fa(g(c)))));
            var newGrate = Optics.Grate<C, D, A2, B2>(NewGrateFunc);
            return (App2<Grates.Mu<A2, B2>, C, D>)(object)newGrate;
        };
    }

    //closed把Grate<A,B,A2,B2>提升为Grate<Func<X,A>,Func<X,B>,A2,B2>
    //创建临时Grate用this作Closed证明eval提升input对应原版Optics.grate(func).eval(this).apply(input)
    public App2<Grates.Mu<A2, B2>, Func<X, A>, Func<X, B>> Closed<A, B, X>(App2<Grates.Mu<A2, B2>, A, B> input)
    {
        var grate = Grates.Unbox<A, B, A2, B2>(input);
        Func<X, B> NewGrateFunc(Func<Func<Func<X, A>, A>, B> f1)
            => x => f1(f2 => f2(x));
        var tempGrate = Optics.Grate<Func<X, A>, Func<X, B>, A, B>(NewGrateFunc);
        var evalFunc = ((Optic<IClosedMu, Func<X, A>, Func<X, B>, A, B>)tempGrate).Eval<Grates.Mu<A2, B2>>(this);
        return evalFunc.Invoke(grate);
    }
}
