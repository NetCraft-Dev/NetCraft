namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Prism容器存放Mu标记避免泛型嵌套
public static class Prisms
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Prism<S,T,A,B>
    public static Prism<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Prism<S, T, A, B>)(object)box!;
}

//Prism棱镜光学对应原版com.mojang.datafixers.optics.Prism
//match尝试分解S为Either<T,A>build从B构造T基于Cocartesian
public interface Prism<S, T, A, B> : App2<Prisms.Mu<A, B>, S, T>, Optic<ICocartesianMu, S, T, A, B>
{
    //match尝试分解S成功返回Right<A>失败返回Left<T>
    Either<T, A> Match(S s);
    //build从B构造T
    T Build(B b);

    //eval用Cocartesian.right把A->B扩展为Either<T,A>->Either<T,B>
    //再dimap用match分解和build重组完成S<->T转换
    Func<App2<P, A, B>, App2<P, S, T>> Optic<ICocartesianMu, S, T, A, B>.Eval<P>(App<ICocartesianMu, P> proof)
    {
        var cocartesian = Cocartesian<P, ICocartesianMu>.Unbox(proof);
        return input => cocartesian.Dimap<Either<T, A>, Either<T, B>, S, T>(
            cocartesian.Right<A, B, T>(input),
            Match,
            either => either.Map(e => e, Build)
        );
    }
}

//Prism具体实现持有match/build委托
internal sealed class PrismImpl<S, T, A, B> : Prism<S, T, A, B>
{
    private readonly Func<S, Either<T, A>> _match;
    private readonly Func<B, T> _build;
    internal PrismImpl(Func<S, Either<T, A>> match, Func<B, T> build)
    {
        _match = match;
        _build = build;
    }
    public Either<T, A> Match(S s) => _match(s);
    public T Build(B b) => _build(b);
}

//Prism作为Cocartesian实例A2/B2固定后dimap/left/right组合match/build
public sealed class PrismInstance<A2, B2> : Cocartesian<Prisms.Mu<A2, B2>, ICocartesianMu>
{
    //dimap用g前处理输入h后处理输出组合原Prism
    public Func<App2<Prisms.Mu<A2, B2>, A, B>, App2<Prisms.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return prismBox => Optics.Prism<C, D, A2, B2>(
            c => Prisms.Unbox<A, B, A2, B2>(prismBox).Match(g(c)).MapLeft(h),
            b2 => h(Prisms.Unbox<A, B, A2, B2>(prismBox).Build(b2))
        );
    }

    //left把Prism扩展到Either左分支保留右分支
    //Map显式指定R2避免C#推断不出Either嵌套类型
    public App2<Prisms.Mu<A2, B2>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<Prisms.Mu<A2, B2>, A, B> input)
    {
        var prism = Prisms.Unbox<A, B, A2, B2>(input);
        return Optics.Prism<Either<A, C>, Either<B, C>, A2, B2>(
            either => either.Map<Either<Either<B, C>, A2>>(
                a => prism.Match(a).MapLeft(b => Either<B, C>.Left(b)),
                c => Either<Either<B, C>, A2>.Left(Either<B, C>.Right(c))
            ),
            b2 => Either<B, C>.Left(prism.Build(b2))
        );
    }

    //right把Prism扩展到Either右分支保留左分支
    public new App2<Prisms.Mu<A2, B2>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<Prisms.Mu<A2, B2>, A, B> input)
    {
        var prism = Prisms.Unbox<A, B, A2, B2>(input);
        return Optics.Prism<Either<C, A>, Either<C, B>, A2, B2>(
            either => either.Map<Either<Either<C, B>, A2>>(
                c => Either<Either<C, B>, A2>.Left(Either<C, B>.Left(c)),
                a => prism.Match(a).MapLeft(b => Either<C, B>.Right(b))
            ),
            b2 => Either<C, B>.Right(prism.Build(b2))
        );
    }
}
