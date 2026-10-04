namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetPs容器存放Mu标记避免泛型嵌套
public static class ReForgetPs
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ReForgetP<R,A,B>
    public static ReForgetP<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetP<R, A, B>)(object)box!;
}

//ReForgetP带双参数的反向遗忘光学对应原版com.mojang.datafixers.optics.ReForgetP
//求值器(A,R)->B双参数Affine写入用
public interface ReForgetP<R, A, B> : App2<ReForgetPs.Mu<R>, A, B>
{
    //run接收A和R返回B
    B Run(A a, R r);

    //Name标识用于调试
    string Name { get; }
}

//ReForgetP具体实现持有委托与name
internal sealed class ReForgetPImpl<R, A, B> : ReForgetP<R, A, B>
{
    private readonly Func<A, R, B> _function;
    private readonly string _name;
    internal ReForgetPImpl(string name, Func<A, R, B> function)
    {
        _function = function;
        _name = name;
    }
    public B Run(A a, R r) => _function(a, r);
    public string Name => _name;
    public override string ToString() => "ReForgetP_" + _name;
}

//ReForgetPInstance作为AffineP实例
//用reForgetP工厂方法构造新ReForgetP包装dimap/first/second/left/right组合
public sealed class ReForgetPInstance<R> : AffineP<ReForgetPs.Mu<R>, ReForgetPInstance<R>.Mu>, App<ReForgetPInstance<R>.Mu, ReForgetPs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ReForgetPInstance<R> InstanceOf = new();
    private ReForgetPInstance() { }

    //dimap用g前处理输入h后处理输出组合原ReForgetP.run
    public Func<App2<ReForgetPs.Mu<R>, A, B>, App2<ReForgetPs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetP<R, C, D>("dimap", (c, r) =>
        {
            var a = g(c);
            var b = ReForgetPs.Unbox<R, A, B>(input).Run(a, r);
            return h(b);
        });
    }

    //first把ReForgetP扩展到Pair第一分量保留C
    public App2<ReForgetPs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Pair<A, C>, Pair<B, C>>("first",
            (p, r) => Pair<B, C>.Of(ReForgetPs.Unbox<R, A, B>(input).Run(p.First, r), p.Second)
        );

    //second把ReForgetP扩展到Pair第二分量保留C
    public new App2<ReForgetPs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Pair<C, A>, Pair<C, B>>("second",
            (p, r) => Pair<C, B>.Of(p.First, ReForgetPs.Unbox<R, A, B>(input).Run(p.Second, r))
        );

    //left把ReForgetP扩展到Either左分支处理A
    public App2<ReForgetPs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Either<A, C>, Either<B, C>>("left",
            (e, r) => e.MapLeft(a => ReForgetPs.Unbox<R, A, B>(input).Run(a, r))
        );

    //right把ReForgetP扩展到Either右分支处理A
    public new App2<ReForgetPs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Either<C, A>, Either<C, B>>("right",
            (e, r) => e.MapRight(a => ReForgetPs.Unbox<R, A, B>(input).Run(a, r))
        );
}
