namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ForgetEs容器存放Mu标记避免泛型嵌套
public static class ForgetEs
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ForgetE<R,A,B>
    public static ForgetE<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ForgetE<R, A, B>)(object)box!;
}

//ForgetE带Either的遗忘光学对应原版com.mojang.datafixers.optics.ForgetE
//求值器A->Either<B,R>允许失败返回Left<B>Affine基于此
public interface ForgetE<R, A, B> : App2<ForgetEs.Mu<R>, A, B>
{
    //run接收A返回Either<B,R>失败为Left<B>成功为Right<R>
    Either<B, R> Run(A a);
}

//ForgetE具体实现持有Func<A,Either<B,R>>委托
internal sealed class ForgetEImpl<R, A, B> : ForgetE<R, A, B>
{
    private readonly Func<A, Either<B, R>> _function;
    internal ForgetEImpl(Func<A, Either<B, R>> function) => _function = function;
    public Either<B, R> Run(A a) => _function(a);
}

//ForgetEInstance作为AffineP实例
//用forgetE工厂方法构造新ForgetE包装dimap/first/second/left/right组合
public sealed class ForgetEInstance<R> : AffineP<ForgetEs.Mu<R>, ForgetEInstance<R>.Mu>, App<ForgetEInstance<R>.Mu, ForgetEs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ForgetEInstance<R> InstanceOf = new();
    private ForgetEInstance() { }

    //dimap用g前处理输入h后处理输出左分支MapLeft组合原ForgetE.run
    public Func<App2<ForgetEs.Mu<R>, A, B>, App2<ForgetEs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.ForgetE<R, C, D>(c => ForgetEs.Unbox<R, A, B>(input).Run(g(c)).MapLeft(h));

    //first扩展到Pair<A,C>左分支包装Pair<B,C>组合原ForgetE.run
    public App2<ForgetEs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Pair<A, C>, Pair<B, C>>(p => ForgetEs.Unbox<R, A, B>(input).Run(p.First).MapLeft(b => Pair<B, C>.Of(b, p.Second)));

    //second扩展到Pair<C,A>左分支包装Pair<C,B>组合原ForgetE.run
    public new App2<ForgetEs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Pair<C, A>, Pair<C, B>>(p => ForgetEs.Unbox<R, A, B>(input).Run(p.Second).MapLeft(b => Pair<C, B>.Of(p.First, b)));

    //left扩展到Either<A,C>左分支保留C右分支直接Left<Either<B,C>.Right<C>>
    public App2<ForgetEs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Either<A, C>, Either<B, C>>(e => e.Map(
            a => ForgetEs.Unbox<R, A, B>(input).Run(a).MapLeft(Either<B, C>.Left),
            c => Either<Either<B, C>, R>.Left(Either<B, C>.Right(c))
        ));

    //right扩展到Either<C,A>右分支保留C左分支直接Left<Either<C,B>.Left<C>>
    public new App2<ForgetEs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Either<C, A>, Either<C, B>>(e => e.Map(
            c => Either<Either<C, B>, R>.Left(Either<C, B>.Left(c)),
            a => ForgetEs.Unbox<R, A, B>(input).Run(a).MapLeft<Either<C, B>>(Either<C, B>.Right)
        ));
}
