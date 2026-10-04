namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetEPs容器存放Mu标记避免泛型嵌套
public static class ReForgetEPs
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ReForgetEP<R,A,B>
    public static ReForgetEP<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetEP<R, A, B>)(object)box!;
}

//ReForgetEP带Either+Pair的反向遗忘光学对应原版com.mojang.datafixers.optics.ReForgetEP
//求值器Either<A,Pair<A,R>>->B分支处理Affine写入用
public interface ReForgetEP<R, A, B> : App2<ReForgetEPs.Mu<R>, A, B>
{
    //run接收Either<A,Pair<A,R>>返回B
    B Run(Either<A, Pair<A, R>> e);

    //Name标识用于调试
    string Name { get; }
}

//ReForgetEP具体实现持有委托与name
internal sealed class ReForgetEPImpl<R, A, B> : ReForgetEP<R, A, B>
{
    private readonly Func<Either<A, Pair<A, R>>, B> _function;
    private readonly string _name;
    internal ReForgetEPImpl(string name, Func<Either<A, Pair<A, R>>, B> function)
    {
        _function = function;
        _name = name;
    }
    public B Run(Either<A, Pair<A, R>> e) => _function(e);
    public string Name => _name;
    public override string ToString() => "ReForgetEP_" + _name;
}

//ReForgetEPInstance作为AffineP实例
//用reForgetEP工厂方法构造新ReForgetEP包装dimap/first/second/left/right组合
public sealed class ReForgetEPInstance<R> : AffineP<ReForgetEPs.Mu<R>, ReForgetEPInstance<R>.Mu>, App<ReForgetEPInstance<R>.Mu, ReForgetEPs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ReForgetEPInstance<R> InstanceOf = new();
    private ReForgetEPInstance() { }

    //dimap用g前处理输入h后处理输出组合原ReForgetEP.run
    public Func<App2<ReForgetEPs.Mu<R>, A, B>, App2<ReForgetEPs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetEP<R, C, D>("dimap", e =>
        {
            var either = e.MapBoth(g, p => Pair<A, R>.Of(g(p.First), p.Second));
            var b = ReForgetEPs.Unbox<R, A, B>(input).Run(either);
            return h(b);
        });
    }

    //first把ReForgetEP扩展到Pair第一分量处理嵌套Pair
    public App2<ReForgetEPs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ReForgetEPs.Mu<R>, A, B> input)
    {
        var reForgetEP = ReForgetEPs.Unbox<R, A, B>(input);
        return Optics.ReForgetEP<R, Pair<A, C>, Pair<B, C>>("first",
            e => e.Map(
                p => Pair<B, C>.Of(reForgetEP.Run(Either<A, Pair<A, R>>.Left(p.First)), p.Second),
                p => Pair<B, C>.Of(reForgetEP.Run(Either<A, Pair<A, R>>.Right(Pair<A, R>.Of(p.First.First, p.Second))), p.First.Second)
            )
        );
    }

    //second把ReForgetEP扩展到Pair第二分量处理嵌套Pair
    public new App2<ReForgetEPs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ReForgetEPs.Mu<R>, A, B> input)
    {
        var reForgetEP = ReForgetEPs.Unbox<R, A, B>(input);
        return Optics.ReForgetEP<R, Pair<C, A>, Pair<C, B>>("second",
            e => e.Map(
                p => Pair<C, B>.Of(p.First, reForgetEP.Run(Either<A, Pair<A, R>>.Left(p.Second))),
                p => Pair<C, B>.Of(p.First.First, reForgetEP.Run(Either<A, Pair<A, R>>.Right(Pair<A, R>.Of(p.First.Second, p.Second))))
            )
        );
    }

    //left把ReForgetEP扩展到Either左分支处理嵌套Either
    public App2<ReForgetEPs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetEPs.Mu<R>, A, B> input)
    {
        var reForgetEP = ReForgetEPs.Unbox<R, A, B>(input);
        return Optics.ReForgetEP<R, Either<A, C>, Either<B, C>>("left",
            e => e.Map(
                e2 => e2.MapLeft(a => reForgetEP.Run(Either<A, Pair<A, R>>.Left(a))),
                p => p.First.MapLeft(a => reForgetEP.Run(Either<A, Pair<A, R>>.Right(Pair<A, R>.Of(a, p.Second))))
            )
        );
    }

    //right把ReForgetEP扩展到Either右分支处理嵌套Either
    public new App2<ReForgetEPs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetEPs.Mu<R>, A, B> input)
    {
        var reForgetEP = ReForgetEPs.Unbox<R, A, B>(input);
        return Optics.ReForgetEP<R, Either<C, A>, Either<C, B>>("right",
            e => e.Map(
                e2 => e2.MapRight(a => reForgetEP.Run(Either<A, Pair<A, R>>.Left(a))),
                p => p.First.MapRight(a => reForgetEP.Run(Either<A, Pair<A, R>>.Right(Pair<A, R>.Of(a, p.Second))))
            )
        );
    }
}
