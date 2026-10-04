namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgets容器存放Mu标记避免泛型嵌套
public static class ReForgets
{
    //二元HKT标记R为求值结果类型
    public sealed class Mu<R> : K2 { }

    //还原类型应用为ReForget<R,A,B>
    public static ReForget<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForget<R, A, B>)(object)box!;
}

//ReForget反向遗忘光学对应原版com.mojang.datafixers.optics.ReForget
//求值器R->B反向于Forget用于写入optic求值
public interface ReForget<R, A, B> : App2<ReForgets.Mu<R>, A, B>
{
    //run接收R返回B
    B Run(R r);
}

//ReForget具体实现持有Func<R,B>委托
internal sealed class ReForgetImpl<R, A, B> : ReForget<R, A, B>
{
    private readonly Func<R, B> _function;
    internal ReForgetImpl(Func<R, B> function) => _function = function;
    public B Run(R r) => _function(r);
}

//ReForgetInstance作为ReCartesian+Cocartesian实例
//用reForget工厂方法构造新ReForget包装dimap/unfirst/unsecond/left/right组合
public sealed class ReForgetInstance<R> : ReCartesian<ReForgets.Mu<R>, ReForgetInstance<R>.Mu>, Cocartesian<ReForgets.Mu<R>, ReForgetInstance<R>.Mu>, App<ReForgetInstance<R>.Mu, ReForgets.Mu<R>>
{
    public sealed class Mu : IReCartesianMu, ICocartesianMu { }
    public static readonly ReForgetInstance<R> InstanceOf = new();
    private ReForgetInstance() { }

    //dimap用h后处理输出组合原ReForget.run
    public Func<App2<ReForgets.Mu<R>, A, B>, App2<ReForgets.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.ReForget<R, C, D>(r => h(ReForgets.Unbox<R, A, B>(input).Run(r)));

    //unfirst从Pair<A,C>取First分量调用原ReForget.run
    public App2<ReForgets.Mu<R>, A, B> Unfirst<A, B, C>(App2<ReForgets.Mu<R>, Pair<A, C>, Pair<B, C>> input)
        => Optics.ReForget<R, A, B>(r => ReForgets.Unbox<R, Pair<A, C>, Pair<B, C>>(input).Run(r).First);

    //unsecond从Pair<C,A>取Second分量调用原ReForget.run
    public App2<ReForgets.Mu<R>, A, B> Unsecond<A, B, C>(App2<ReForgets.Mu<R>, Pair<C, A>, Pair<C, B>> input)
        => Optics.ReForget<R, A, B>(r => ReForgets.Unbox<R, Pair<C, A>, Pair<C, B>>(input).Run(r).Second);

    //left把ReForget扩展到Either左分支包装Left值
    public App2<ReForgets.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgets.Mu<R>, A, B> input)
        => Optics.ReForget<R, Either<A, C>, Either<B, C>>(r => Either<B, C>.Left(ReForgets.Unbox<R, A, B>(input).Run(r)));

    //right把ReForget扩展到Either右分支包装Right值
    public new App2<ReForgets.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgets.Mu<R>, A, B> input)
        => Optics.ReForget<R, Either<C, A>, Either<C, B>>(r => Either<C, B>.Right(ReForgets.Unbox<R, A, B>(input).Run(r)));
}
