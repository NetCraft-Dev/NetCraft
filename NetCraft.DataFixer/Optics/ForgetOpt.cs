namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ForgetOpts容器存放Mu标记避免泛型嵌套
public static class ForgetOpts
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ForgetOpt<R,A,B>
    public static ForgetOpt<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ForgetOpt<R, A, B>)(object)box!;
}

//ForgetOpt带Optional的遗忘光学对应原版com.mojang.datafixers.optics.ForgetOpt
//求值器A->Optional<R>可能为空Affine基于此
public interface ForgetOpt<R, A, B> : App2<ForgetOpts.Mu<R>, A, B>
{
    //run接收A返回Optional<R>可能为Empty
    NetCraft.Codec.Optional<R> Run(A a);
}

//ForgetOpt具体实现持有Func<A,Optional<R>>委托
internal sealed class ForgetOptImpl<R, A, B> : ForgetOpt<R, A, B>
{
    private readonly Func<A, NetCraft.Codec.Optional<R>> _function;
    internal ForgetOptImpl(Func<A, NetCraft.Codec.Optional<R>> function) => _function = function;
    public NetCraft.Codec.Optional<R> Run(A a) => _function(a);
}

//ForgetOptInstance作为AffineP实例
//用forgetOpt工厂方法构造新ForgetOpt包装dimap/first/second/left/right组合
public sealed class ForgetOptInstance<R> : AffineP<ForgetOpts.Mu<R>, ForgetOptInstance<R>.Mu>, App<ForgetOptInstance<R>.Mu, ForgetOpts.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ForgetOptInstance<R> InstanceOf = new();
    private ForgetOptInstance() { }

    //dimap用g前处理输入直接调用原ForgetOpt.run忽略h
    public Func<App2<ForgetOpts.Mu<R>, A, B>, App2<ForgetOpts.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.ForgetOpt<R, C, D>(c => ForgetOpts.Unbox<R, A, B>(input).Run(g(c)));

    //first扩展到Pair<A,C>取First分量调用原ForgetOpt.run
    public App2<ForgetOpts.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Pair<A, C>, Pair<B, C>>(p => ForgetOpts.Unbox<R, A, B>(input).Run(p.First));

    //second扩展到Pair<C,A>取Second分量调用原ForgetOpt.run
    public new App2<ForgetOpts.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Pair<C, A>, Pair<C, B>>(p => ForgetOpts.Unbox<R, A, B>(input).Run(p.Second));

    //left扩展到Either<A,C>左分支flatMap原ForgetOpt.run右分支返回Empty
    public App2<ForgetOpts.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Either<A, C>, Either<B, C>>(e => e.GetLeft().FlatMap(a => ForgetOpts.Unbox<R, A, B>(input).Run(a)));

    //right扩展到Either<C,A>右分支flatMap原ForgetOpt.run左分支返回Empty
    public new App2<ForgetOpts.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Either<C, A>, Either<C, B>>(e => e.GetRight().FlatMap(a => ForgetOpts.Unbox<R, A, B>(input).Run(a)));
}
