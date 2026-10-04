namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;

//Getters容器存放Mu标记避免泛型嵌套
public static class Getters
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Getter<S,T,A,B>
    public static Getter<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Getter<S, T, A, B>)(object)box!;
}

//Getter获取光学对应原版com.mojang.datafixers.optics.Getter
//只读view取焦点基于GetterP(Profunctor+Bicontravariant)
public interface Getter<S, T, A, B> : App2<Getters.Mu<A, B>, S, T>, Optic<IGetterPMu, S, T, A, B>
{
    //get从S取焦点A
    A Get(S s);

    //eval用GetterP.secondPhantom附加phantom第二分量后lmap用get组合
    //SecondPhantom返回App2<P,A,A>右分量被phantom强转为App2<P,S,T>
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IGetterPMu, S, T, A, B>.Eval<P>(App<IGetterPMu, P> proof)
    {
        var getterP = GetterP<P, IGetterPMu>.Unbox(proof);
        return input => (App2<P, S, T>)(object)getterP.Lmap<A, A, S>(getterP.SecondPhantom<A, B, A>(input), Get);
    }
}

//Getter具体实现持有get委托
internal sealed class GetterImpl<S, T, A, B> : Getter<S, T, A, B>
{
    private readonly Func<S, A> _get;
    internal GetterImpl(Func<S, A> get) => _get = get;
    public A Get(S s) => _get(s);
}

//GetterInstance作为GetterP实例
//dimap用g前处理输入组合原Getter.getcimap用Supplier延迟获取
public sealed class GetterInstance<A2, B2> : GetterP<Getters.Mu<A2, B2>, IGetterPMu>
{
    //dimap用g前处理输入再调用原Getter.get
    public Func<App2<Getters.Mu<A2, B2>, A, B>, App2<Getters.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.Getter<C, D, A2, B2>(c => Getters.Unbox<A, B, A2, B2>(input).Get(g(c)));

    //cimap用Supplier延迟获取原Getter.get组合g前处理
    public Func<Func<App2<Getters.Mu<A2, B2>, A, B>>, App2<Getters.Mu<A2, B2>, C, D>> Cimap<A, B, C, D>(Func<C, A> g, Func<D, B> h)
        => input => Optics.Getter<C, D, A2, B2>(c => Getters.Unbox<A, B, A2, B2>(input()).Get(g(c)));
}
