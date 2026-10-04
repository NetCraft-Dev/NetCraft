namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;

//Adapter容器存放Mu标记避免泛型嵌套
public static class Adapters
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Adapter<S,T,A,B>
    public static Adapter<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Adapter<S, T, A, B>)(object)box!;
}

//Adapter适配器光学对应原版com.mojang.datafixers.optics.Adapter
//最简单光学from/to直接转换S->A与B->T
//用IProfunctorMu替代原版Profunctor.Mu作为Proof标记
public interface Adapter<S, T, A, B> : App2<Adapters.Mu<A, B>, S, T>, Optic<IProfunctorMu, S, T, A, B>
{
    //从S取焦点A
    A From(S s);
    //把B放回得T
    T To(B b);

    //eval用Profunctor.dimap组合from/to
    //显式指定Dimap方法类型参数对应Adapter的A/B/S/T避免推断歧义
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IProfunctorMu, S, T, A, B>.Eval<P>(App<IProfunctorMu, P> proof)
    {
        var profunctor = Profunctor<P, IProfunctorMu>.Unbox(proof);
        return a => profunctor!.Dimap<A, B, S, T>(a, From, To);
    }
}

//Adapter具体实现持有from/to委托
internal sealed class AdapterImpl<S, T, A, B> : Adapter<S, T, A, B>
{
    private readonly Func<S, A> _from;
    private readonly Func<B, T> _to;
    internal AdapterImpl(Func<S, A> from, Func<B, T> to)
    {
        _from = from;
        _to = to;
    }
    public A From(S s) => _from(s);
    public T To(B b) => _to(b);
}

//Adapter作为Profunctor实例A2/B2固定后dimap组合from/to
public sealed class AdapterInstance<A2, B2> : Profunctor<Adapters.Mu<A2, B2>, IProfunctorMu>
{
    //dimap用g逆映射输入h正映射输出构造新Adapter
    //Unbox指定<A,B,A2,B2>因为Mu<A2,B2>包装的Adapter焦点/新值类型固定为A2/B2
    public Func<App2<Adapters.Mu<A2, B2>, A, B>, App2<Adapters.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return a => Optics.Adapter<C, D, A2, B2>(
            c => Adapters.Unbox<A, B, A2, B2>(a).From(g(c)),
            b2 => h(Adapters.Unbox<A, B, A2, B2>(a).To(b2))
        );
    }
}
