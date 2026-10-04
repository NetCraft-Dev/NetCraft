namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Affine容器存放Mu标记避免泛型嵌套
public static class Affines
{
    //二元HKT标记A/B为焦点/新值类型
    public sealed class Mu<A, B> : K2 { }

    //还原类型应用为Affine<S,T,A,B>
    public static Affine<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
        => (Affine<S, T, A, B>)(object)box!;
}

//Affine仿射光学对应原版com.mojang.datafixers.optics.Affine
//preview尝试取焦点set替换焦点基于AffineP=Cartesian+Cocartesian
public interface Affine<S, T, A, B> : App2<Affines.Mu<A, B>, S, T>, Optic<IAffinePMu, S, T, A, B>
{
    //preview尝试取焦点成功返回Right<A>失败返回Left<T>
    Either<T, A> Preview(S s);
    //set用B替换焦点得T保留S其他部分
    T Set(B b, S s);

    //eval用Cartesian.first扩展Pair再rmap用set替换再用Cocartesian.left加分支
    //最后dimap用preview分解和Either.unwrap合并完成S<->T
    //Dimap类型参数显式指定避免C#lambda推断失败
    //left的C=T使h可调Either<T,T>.Unwrap合并左右
    Func<App2<P, A, B>, App2<P, S, T>> Optic<IAffinePMu, S, T, A, B>.Eval<P>(App<IAffinePMu, P> proof)
    {
        var cartesian = Cartesian<P, IAffinePMu>.Unbox(proof);
        var cocartesian = Cocartesian<P, IAffinePMu>.Unbox(proof);
        return input => cartesian.Dimap<Either<Pair<A, S>, T>, Either<T, T>, S, T>(
            cocartesian.Left<Pair<A, S>, T, T>(
                cartesian.Rmap<Pair<A, S>, Pair<B, S>, T>(
                    cartesian.First<A, B, S>(input),
                    p => Set(p.First, p.Second)
                )
            ),
            s => Preview(s).Map(
                t => Either<Pair<A, S>, T>.Right(t),
                a => Either<Pair<A, S>, T>.Left(Pair<A, S>.Of(a, s))
            ),
            Either<T, T>.Unwrap
        );
    }
}

//Affine具体实现持有preview/set委托
internal sealed class AffineImpl<S, T, A, B> : Affine<S, T, A, B>
{
    private readonly Func<S, Either<T, A>> _preview;
    private readonly Func<B, S, T> _set;
    internal AffineImpl(Func<S, Either<T, A>> preview, Func<B, S, T> set)
    {
        _preview = preview;
        _set = set;
    }
    public Either<T, A> Preview(S s) => _preview(s);
    public T Set(B b, S s) => _set(b, s);
}

//Affine作为AffineP实例A2/B2固定后dimap/first/second/left/right组合preview/set
public sealed class AffineInstance<A2, B2> : AffineP<Affines.Mu<A2, B2>, IAffinePMu>
{
    //dimap用g前处理输入h后处理输出组合原Affine
    public Func<App2<Affines.Mu<A2, B2>, A, B>, App2<Affines.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return affineBox => Optics.Affine<C, D, A2, B2>(
            c => Affines.Unbox<A, B, A2, B2>(affineBox).Preview(g(c)).MapLeft(h),
            (b2, c) => h(Affines.Unbox<A, B, A2, B2>(affineBox).Set(b2, g(c)))
        );
    }

    //first把Affine扩展到Pair第一分量保留第二分量
    public App2<Affines.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Affines.Mu<A2, B2>, A, B> input)
    {
        var affine = Affines.Unbox<A, B, A2, B2>(input);
        return Optics.Affine<Pair<A, C>, Pair<B, C>, A2, B2>(
            pair => affine.Preview(pair.First).MapBoth(b => Pair<B, C>.Of(b, pair.Second), a => a),
            (b2, pair) => Pair<B, C>.Of(affine.Set(b2, pair.First), pair.Second)
        );
    }

    //left把Affine扩展到Either左分支保留右分支
    public App2<Affines.Mu<A2, B2>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<Affines.Mu<A2, B2>, A, B> input)
    {
        var affine = Affines.Unbox<A, B, A2, B2>(input);
        return Optics.Affine<Either<A, C>, Either<B, C>, A2, B2>(
            either => either.Map(
                a => affine.Preview(a).MapLeft(b => Either<B, C>.Left(b)),
                c => Either<Either<B, C>, A2>.Left(Either<B, C>.Right(c))
            ),
            (b, either) => either.Map(
                l => Either<B, C>.Left(affine.Set(b, l)),
                c => Either<B, C>.Right(c)
            )
        );
    }
}
