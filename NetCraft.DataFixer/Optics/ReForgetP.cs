namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetPs container holding the Mu marker, avoiding generic nesting
public static class ReForgetPs
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ReForgetP<R,A,B>
    public static ReForgetP<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetP<R, A, B>)(object)box!;
}

//ReForgetP reverse forgetful optic with two parameters, maps to vanilla com.mojang.datafixers.optics.ReForgetP
//evaluator (A,R)->B with two parameters, used for Affine writes
public interface ReForgetP<R, A, B> : App2<ReForgetPs.Mu<R>, A, B>
{
    //run takes A and R and returns B
    B Run(A a, R r);

    //Name identifier used for debugging
    string Name { get; }
}

//ReForgetP concrete implementation holding the delegate and name
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

//ReForgetPInstance as the AffineP instance
//uses the reForgetP factory to build a new ReForgetP wrapping the dimap/first/second/left/right combination
public sealed class ReForgetPInstance<R> : AffineP<ReForgetPs.Mu<R>, ReForgetPInstance<R>.Mu>, App<ReForgetPInstance<R>.Mu, ReForgetPs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ReForgetPInstance<R> InstanceOf = new();
    private ReForgetPInstance() { }

    //dimap preprocesses the input with g and postprocesses the output with h, composing the original ReForgetP.run
    public Func<App2<ReForgetPs.Mu<R>, A, B>, App2<ReForgetPs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetP<R, C, D>("dimap", (c, r) =>
        {
            var a = g(c);
            var b = ReForgetPs.Unbox<R, A, B>(input).Run(a, r);
            return h(b);
        });
    }

    //first extends ReForgetP to the first component of Pair, preserving C
    public App2<ReForgetPs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Pair<A, C>, Pair<B, C>>("first",
            (p, r) => Pair<B, C>.Of(ReForgetPs.Unbox<R, A, B>(input).Run(p.First, r), p.Second)
        );

    //second extends ReForgetP to the second component of Pair, preserving C
    public new App2<ReForgetPs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Pair<C, A>, Pair<C, B>>("second",
            (p, r) => Pair<C, B>.Of(p.First, ReForgetPs.Unbox<R, A, B>(input).Run(p.Second, r))
        );

    //left extends ReForgetP to the either left branch, handling A
    public App2<ReForgetPs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Either<A, C>, Either<B, C>>("left",
            (e, r) => e.MapLeft(a => ReForgetPs.Unbox<R, A, B>(input).Run(a, r))
        );

    //right extends ReForgetP to the either right branch, handling A
    public new App2<ReForgetPs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetPs.Mu<R>, A, B> input)
        => Optics.ReForgetP<R, Either<C, A>, Either<C, B>>("right",
            (e, r) => e.MapRight(a => ReForgetPs.Unbox<R, A, B>(input).Run(a, r))
        );
}
