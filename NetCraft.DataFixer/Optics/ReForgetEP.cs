namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetEPs container holding the Mu marker, avoiding generic nesting
public static class ReForgetEPs
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ReForgetEP<R,A,B>
    public static ReForgetEP<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetEP<R, A, B>)(object)box!;
}

//ReForgetEP reverse forgetful optic with Either+Pair, maps to vanilla com.mojang.datafixers.optics.ReForgetEP
//evaluator Either<A,Pair<A,R>>->B with branch handling, used for Affine writes
public interface ReForgetEP<R, A, B> : App2<ReForgetEPs.Mu<R>, A, B>
{
    //run takes Either<A,Pair<A,R>> and returns B
    B Run(Either<A, Pair<A, R>> e);

    //Name identifier used for debugging
    string Name { get; }
}

//ReForgetEP concrete implementation holding the delegate and name
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

//ReForgetEPInstance as the AffineP instance
//uses the reForgetEP factory to build a new ReForgetEP wrapping the dimap/first/second/left/right combination
public sealed class ReForgetEPInstance<R> : AffineP<ReForgetEPs.Mu<R>, ReForgetEPInstance<R>.Mu>, App<ReForgetEPInstance<R>.Mu, ReForgetEPs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ReForgetEPInstance<R> InstanceOf = new();
    private ReForgetEPInstance() { }

    //dimap preprocesses the input with g and postprocesses the output with h, composing the original ReForgetEP.run
    public Func<App2<ReForgetEPs.Mu<R>, A, B>, App2<ReForgetEPs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetEP<R, C, D>("dimap", e =>
        {
            var either = e.MapBoth(g, p => Pair<A, R>.Of(g(p.First), p.Second));
            var b = ReForgetEPs.Unbox<R, A, B>(input).Run(either);
            return h(b);
        });
    }

    //first extends ReForgetEP to the first component of Pair, handling the nested Pair
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

    //second extends ReForgetEP to the second component of Pair, handling the nested Pair
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

    //left extends ReForgetEP to the either left branch, handling the nested Either
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

    //right extends ReForgetEP to the either right branch, handling the nested Either
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
