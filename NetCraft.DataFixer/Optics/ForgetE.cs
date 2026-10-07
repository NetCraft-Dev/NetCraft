namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ForgetEs container holding the Mu marker, avoiding generic nesting
public static class ForgetEs
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ForgetE<R,A,B>
    public static ForgetE<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ForgetE<R, A, B>)(object)box!;
}

//ForgetE forgetful optic with Either, maps to vanilla com.mojang.datafixers.optics.ForgetE
//evaluator A->Either<B,R>, allowing failure by returning Left<B>; Affine is based on this
public interface ForgetE<R, A, B> : App2<ForgetEs.Mu<R>, A, B>
{
    //run takes A and returns Either<B,R>: Left<B> on failure, Right<R> on success
    Either<B, R> Run(A a);
}

//ForgetE concrete implementation holding a Func<A,Either<B,R>> delegate
internal sealed class ForgetEImpl<R, A, B> : ForgetE<R, A, B>
{
    private readonly Func<A, Either<B, R>> _function;
    internal ForgetEImpl(Func<A, Either<B, R>> function) => _function = function;
    public Either<B, R> Run(A a) => _function(a);
}

//ForgetEInstance as the AffineP instance
//uses the forgetE factory to build a new ForgetE wrapping the dimap/first/second/left/right combination
public sealed class ForgetEInstance<R> : AffineP<ForgetEs.Mu<R>, ForgetEInstance<R>.Mu>, App<ForgetEInstance<R>.Mu, ForgetEs.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ForgetEInstance<R> InstanceOf = new();
    private ForgetEInstance() { }

    //dimap preprocesses the input with g and postprocesses the output with h, composing the original ForgetE.run with MapLeft on the left branch
    public Func<App2<ForgetEs.Mu<R>, A, B>, App2<ForgetEs.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.ForgetE<R, C, D>(c => ForgetEs.Unbox<R, A, B>(input).Run(g(c)).MapLeft(h));

    //first extends to Pair<A,C>, wrapping the left branch as Pair<B,C> to compose the original ForgetE.run
    public App2<ForgetEs.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Pair<A, C>, Pair<B, C>>(p => ForgetEs.Unbox<R, A, B>(input).Run(p.First).MapLeft(b => Pair<B, C>.Of(b, p.Second)));

    //second extends to Pair<C,A>, wrapping the left branch as Pair<C,B> to compose the original ForgetE.run
    public new App2<ForgetEs.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Pair<C, A>, Pair<C, B>>(p => ForgetEs.Unbox<R, A, B>(input).Run(p.Second).MapLeft(b => Pair<C, B>.Of(p.First, b)));

    //left extends to Either<A,C>: the left branch preserves C, the right branch directly returns Left<Either<B,C>.Right<C>>
    public App2<ForgetEs.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Either<A, C>, Either<B, C>>(e => e.Map(
            a => ForgetEs.Unbox<R, A, B>(input).Run(a).MapLeft(Either<B, C>.Left),
            c => Either<Either<B, C>, R>.Left(Either<B, C>.Right(c))
        ));

    //right extends to Either<C,A>: the right branch preserves C, the left branch directly returns Left<Either<C,B>.Left<C>>
    public new App2<ForgetEs.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ForgetEs.Mu<R>, A, B> input)
        => Optics.ForgetE<R, Either<C, A>, Either<C, B>>(e => e.Map(
            c => Either<Either<C, B>, R>.Left(Either<C, B>.Left(c)),
            a => ForgetEs.Unbox<R, A, B>(input).Run(a).MapLeft<Either<C, B>>(Either<C, B>.Right)
        ));
}
