namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetCs容器存放Mu标记避免泛型嵌套
public static class ReForgetCs
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ReForgetC<R,A,B>
    public static ReForgetC<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetC<R, A, B>)(object)box!;
}

//ReForgetC合并Either<Func<R,B>,Func<A,R,B>>的反向遗忘光学对应原版com.mojang.datafixers.optics.ReForgetC
//impl返回Either左忽略A右使用A的双参数模式Affine写入用
public interface ReForgetC<R, A, B> : App2<ReForgetCs.Mu<R>, A, B>
{
    //impl返回Either<Func<R,B>忽略A或Func<A,R,B>使用A
    Either<Func<R, B>, Func<A, R, B>> Impl();

    //Name标识用于调试
    string Name { get; }
}

//ReForgetC具体实现持有Either委托与name
internal sealed class ReForgetCImpl<R, A, B> : ReForgetC<R, A, B>
{
    private readonly Either<Func<R, B>, Func<A, R, B>> _impl;
    private readonly string _name;
    internal ReForgetCImpl(string name, Either<Func<R, B>, Func<A, R, B>> impl)
    {
        _impl = impl;
        _name = name;
    }
    public Either<Func<R, B>, Func<A, R, B>> Impl() => _impl;
    public string Name => _name;
    public override string ToString() => "ReForgetC_" + _name;
}

//ReForgetCInstance作为AffineP实例
//用reForgetC工厂方法构造新ReForgetC包装dimap/first/second/left/right组合
public sealed class ReForgetCInstance<R> : AffineP<ReForgetCs.Mu<R>, ReForgetCInstance<R>.Mu>, App<ReForgetCInstance<R>.Mu, ReForgetCs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ReForgetCInstance<R> InstanceOf = new();
    private ReForgetCInstance() { }

    //dimap用g前处理输入h后处理输出组合impl分支
    public Func<App2<ReForgetCs.Mu<R>, A, B>, App2<ReForgetCs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetC<R, C, D>("dimap",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, D>, Func<C, R, D>>.Left(r => h(f(r))),
                f => Either<Func<R, D>, Func<C, R, D>>.Right((c, r) => h(f(g(c), r)))
            )
        );
    }

    //first切换impl从Left忽略A到Right使用A
    public App2<ReForgetCs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Pair<A, C>, Pair<B, C>>("first",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Pair<B, C>>, Func<Pair<A, C>, R, Pair<B, C>>>.Right((p, r) => Pair<B, C>.Of(f(r), p.Second)),
                f => Either<Func<R, Pair<B, C>>, Func<Pair<A, C>, R, Pair<B, C>>>.Right((p, r) => Pair<B, C>.Of(f(p.First, r), p.Second))
            )
        );

    //second切换impl从Left忽略A到Right使用A
    public new App2<ReForgetCs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Pair<C, A>, Pair<C, B>>("second",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Pair<C, B>>, Func<Pair<C, A>, R, Pair<C, B>>>.Right((p, r) => Pair<C, B>.Of(p.First, f(r))),
                f => Either<Func<R, Pair<C, B>>, Func<Pair<C, A>, R, Pair<C, B>>>.Right((p, r) => Pair<C, B>.Of(p.First, f(p.Second, r)))
            )
        );

    //left保持impl模式Left继续prism路径Right映射Either左分支
    public App2<ReForgetCs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Either<A, C>, Either<B, C>>("left",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Either<B, C>>, Func<Either<A, C>, R, Either<B, C>>>.Left(r => Either<B, C>.Left(f(r))),
                f => Either<Func<R, Either<B, C>>, Func<Either<A, C>, R, Either<B, C>>>.Right((p, r) => p.MapLeft(a => f(a, r)))
            )
        );

    //right保持impl模式Left继续prism路径Right映射Either右分支
    public new App2<ReForgetCs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Either<C, A>, Either<C, B>>("right",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Either<C, B>>, Func<Either<C, A>, R, Either<C, B>>>.Left(r => Either<C, B>.Right(f(r))),
                f => Either<Func<R, Either<C, B>>, Func<Either<C, A>, R, Either<C, B>>>.Right((p, r) => p.MapRight(a => f(a, r)))
            )
        );
}
