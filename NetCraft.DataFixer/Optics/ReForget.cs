namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgets container holding the Mu marker, avoiding generic nesting
public static class ReForgets
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ReForget<R,A,B>
    public static ReForget<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForget<R, A, B>)(object)box!;
}

//ReForget reverse forgetful optic maps to vanilla com.mojang.datafixers.optics.ReForget
//evaluator R->B, the reverse of Forget, used for write-optic evaluation
public interface ReForget<R, A, B> : App2<ReForgets.Mu<R>, A, B>
{
    //run takes R and returns B
    B Run(R r);
}

//ReForget concrete implementation holding a Func<R,B> delegate
internal sealed class ReForgetImpl<R, A, B> : ReForget<R, A, B>
{
    private readonly Func<R, B> _function;
    internal ReForgetImpl(Func<R, B> function) => _function = function;
    public B Run(R r) => _function(r);
}

//ReForgetInstance as the ReCartesian+Cocartesian instance
//uses the reForget factory to build a new ReForget wrapping the dimap/unfirst/unsecond/left/right combination
public sealed class ReForgetInstance<R> : ReCartesian<ReForgets.Mu<R>, ReForgetInstance<R>.Mu>, Cocartesian<ReForgets.Mu<R>, ReForgetInstance<R>.Mu>, App<ReForgetInstance<R>.Mu, ReForgets.Mu<R>>
{
    public sealed class Mu : IReCartesianMu, ICocartesianMu { }
    public static readonly ReForgetInstance<R> InstanceOf = new();
    private ReForgetInstance() { }

    //dimap postprocesses the output with h, composing the original ReForget.run
    public Func<App2<ReForgets.Mu<R>, A, B>, App2<ReForgets.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.ReForget<R, C, D>(r => h(ReForgets.Unbox<R, A, B>(input).Run(r)));

    //unfirst takes the First component from Pair<A,C> and calls the original ReForget.run
    public App2<ReForgets.Mu<R>, A, B> Unfirst<A, B, C>(App2<ReForgets.Mu<R>, Pair<A, C>, Pair<B, C>> input)
        => Optics.ReForget<R, A, B>(r => ReForgets.Unbox<R, Pair<A, C>, Pair<B, C>>(input).Run(r).First);

    //unsecond takes the Second component from Pair<C,A> and calls the original ReForget.run
    public App2<ReForgets.Mu<R>, A, B> Unsecond<A, B, C>(App2<ReForgets.Mu<R>, Pair<C, A>, Pair<C, B>> input)
        => Optics.ReForget<R, A, B>(r => ReForgets.Unbox<R, Pair<C, A>, Pair<C, B>>(input).Run(r).Second);

    //left extends ReForget to the either left branch, wrapping the Left value
    public App2<ReForgets.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgets.Mu<R>, A, B> input)
        => Optics.ReForget<R, Either<A, C>, Either<B, C>>(r => Either<B, C>.Left(ReForgets.Unbox<R, A, B>(input).Run(r)));

    //right extends ReForget to the either right branch, wrapping the Right value
    public new App2<ReForgets.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgets.Mu<R>, A, B> input)
        => Optics.ReForget<R, Either<C, A>, Either<C, B>>(r => Either<C, B>.Right(ReForgets.Unbox<R, A, B>(input).Run(r)));
}
