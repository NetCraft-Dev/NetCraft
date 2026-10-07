namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ReForgetCs container holding the Mu marker, avoiding generic nesting
public static class ReForgetCs
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ReForgetC<R,A,B>
    public static ReForgetC<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ReForgetC<R, A, B>)(object)box!;
}

//ReForgetC reverse forgetful optic merging Either<Func<R,B>,Func<A,R,B>>, maps to vanilla com.mojang.datafixers.optics.ReForgetC
//impl returns Either: the left ignores A, the right uses A (two-parameter mode), used by Affine writes
public interface ReForgetC<R, A, B> : App2<ReForgetCs.Mu<R>, A, B>
{
    //impl returns Either<Func<R,B> ignoring A, or Func<A,R,B> using A>
    Either<Func<R, B>, Func<A, R, B>> Impl();

    //Name identifier used for debugging
    string Name { get; }
}

//ReForgetC concrete implementation holding the Either delegate and name
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

//ReForgetCInstance as the AffineP instance
//uses the reForgetC factory to build a new ReForgetC wrapping the dimap/first/second/left/right combination
public sealed class ReForgetCInstance<R> : AffineP<ReForgetCs.Mu<R>, ReForgetCInstance<R>.Mu>, App<ReForgetCInstance<R>.Mu, ReForgetCs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ReForgetCInstance<R> InstanceOf = new();
    private ReForgetCInstance() { }

    //dimap preprocesses the input with g and postprocesses the output with h, composing the impl branch
    public Func<App2<ReForgetCs.Mu<R>, A, B>, App2<ReForgetCs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
    {
        return input => Optics.ReForgetC<R, C, D>("dimap",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, D>, Func<C, R, D>>.Left(r => h(f(r))),
                f => Either<Func<R, D>, Func<C, R, D>>.Right((c, r) => h(f(g(c), r)))
            )
        );
    }

    //first switches impl from Left (ignoring A) to Right (using A)
    public App2<ReForgetCs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Pair<A, C>, Pair<B, C>>("first",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Pair<B, C>>, Func<Pair<A, C>, R, Pair<B, C>>>.Right((p, r) => Pair<B, C>.Of(f(r), p.Second)),
                f => Either<Func<R, Pair<B, C>>, Func<Pair<A, C>, R, Pair<B, C>>>.Right((p, r) => Pair<B, C>.Of(f(p.First, r), p.Second))
            )
        );

    //second switches impl from Left (ignoring A) to Right (using A)
    public new App2<ReForgetCs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Pair<C, A>, Pair<C, B>>("second",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Pair<C, B>>, Func<Pair<C, A>, R, Pair<C, B>>>.Right((p, r) => Pair<C, B>.Of(p.First, f(r))),
                f => Either<Func<R, Pair<C, B>>, Func<Pair<C, A>, R, Pair<C, B>>>.Right((p, r) => Pair<C, B>.Of(p.First, f(p.Second, r)))
            )
        );

    //left keeps the impl mode: Left continues the prism path, Right maps the Either left branch
    public App2<ReForgetCs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Either<A, C>, Either<B, C>>("left",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Either<B, C>>, Func<Either<A, C>, R, Either<B, C>>>.Left(r => Either<B, C>.Left(f(r))),
                f => Either<Func<R, Either<B, C>>, Func<Either<A, C>, R, Either<B, C>>>.Right((p, r) => p.MapLeft(a => f(a, r)))
            )
        );

    //right keeps the impl mode: Left continues the prism path, Right maps the Either right branch
    public new App2<ReForgetCs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ReForgetCs.Mu<R>, A, B> input)
        => Optics.ReForgetC<R, Either<C, A>, Either<C, B>>("right",
            ReForgetCs.Unbox<R, A, B>(input).Impl().Map(
                f => Either<Func<R, Either<C, B>>, Func<Either<C, A>, R, Either<C, B>>>.Left(r => Either<C, B>.Right(f(r))),
                f => Either<Func<R, Either<C, B>>, Func<Either<C, A>, R, Either<C, B>>>.Right((p, r) => p.MapRight(a => f(a, r)))
            )
        );
}
