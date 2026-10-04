namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetEs容器存放Mu标记避免泛型嵌套
public static class ReForgetEs
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ReForgetE<R,A,B>
    public static ReForgetE<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetE<R, A, B>)(object)box!;
}

//ReForgetE带Either的反向遗忘光学对应原版com.mojang.datafixers.optics.ReForgetE
//求值器Either<A,R>->B分支处理Prism写入用
public interface ReForgetE<R, A, B> : App2<ReForgetEs.Mu<R>, A, B>
{
    //run接收Either<A,R>返回B
    B Run(Either<A, R> r);

    //ToString标识name用于调试
    string Name { get; }
}

//ReForgetE具体实现持有委托与name
internal sealed class ReForgetEImpl<R, A, B> : ReForgetE<R, A, B>
{
    private readonly Func<Either<A, R>, B> _function;
    private readonly string _name;
    internal ReForgetEImpl(string name, Func<Either<A, R>, B> function)
    {
        _function = function;
        _name = name;
    }
    public B Run(Either<A, R> r) => _function(r);
    public string Name => _name;
    public override string ToString() => "ReForgetE_" + _name;
}

//ReForgetEInstance作为Cocartesian实例
//用reForgetE工厂方法构造新ReForgetE包装dimap/left/right组合
public sealed class ReForgetEInstance<R> : Cocartesian<ReForgetEs.Mu<R>, ReForgetEInstance<R>.Mu>, App<ReForgetEInstance<R>.Mu, ReForgetEs.Mu<R>>
{
    public sealed class Mu : ICocartesianMu { }
    public static readonly ReForgetEInstance<R> InstanceOf = new();
    private ReForgetEInstance() { }

    //dimap用g前处理输入h后处理输出组合原ReForgetE.run
    public Func<App2<ReForgetEs.Mu<R>, A, B>, App2<ReForgetEs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetE<R, C, D>("dimap", e =>
        {
            var either = e.MapLeft(g);
            var b = ReForgetEs.Unbox<R, A, B>(input).Run(either);
            return h(b);
        });
    }

    //left把ReForgetE扩展到Either左分支处理嵌套Either
    public App2<ReForgetEs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetEs.Mu<R>, A, B> input)
    {
        var reForgetE = ReForgetEs.Unbox<R, A, B>(input);
        return Optics.ReForgetE<R, Either<A, C>, Either<B, C>>("left",
            e => e.Map(
                e2 => e2.Map(
                    a => Either<B, C>.Left(reForgetE.Run(Either<A, R>.Left(a))),
                    Either<B, C>.Right
                ),
                r => Either<B, C>.Left(reForgetE.Run(Either<A, R>.Right(r)))
            )
        );
    }

    //right把ReForgetE扩展到Either右分支处理嵌套Either
    public new App2<ReForgetEs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetEs.Mu<R>, A, B> input)
    {
        var reForgetE = ReForgetEs.Unbox<R, A, B>(input);
        return Optics.ReForgetE<R, Either<C, A>, Either<C, B>>("right",
            e => e.Map(
                e2 => e2.Map(
                    Either<C, B>.Left,
                    a => Either<C, B>.Right(reForgetE.Run(Either<A, R>.Left(a)))
                ),
                r => Either<C, B>.Right(reForgetE.Run(Either<A, R>.Right(r)))
            )
        );
    }
}
