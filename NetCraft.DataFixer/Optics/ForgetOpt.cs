namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//ForgetOpts container holding the Mu marker, avoiding generic nesting
public static class ForgetOpts
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as ForgetOpt<R,A,B>
    public static ForgetOpt<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (ForgetOpt<R, A, B>)(object)box!;
}

//ForgetOpt forgetful optic with Optional, maps to vanilla com.mojang.datafixers.optics.ForgetOpt
//evaluator A->Optional<R>, possibly empty; Affine is based on this
public interface ForgetOpt<R, A, B> : App2<ForgetOpts.Mu<R>, A, B>
{
    //run takes A and returns Optional<R>, possibly Empty
    NetCraft.Codec.Optional<R> Run(A a);
}

//ForgetOpt concrete implementation holding a Func<A,Optional<R>> delegate
internal sealed class ForgetOptImpl<R, A, B> : ForgetOpt<R, A, B>
{
    private readonly Func<A, NetCraft.Codec.Optional<R>> _function;
    internal ForgetOptImpl(Func<A, NetCraft.Codec.Optional<R>> function) => _function = function;
    public NetCraft.Codec.Optional<R> Run(A a) => _function(a);
}

//ForgetOptInstance as the AffineP instance
//uses the forgetOpt factory to build a new ForgetOpt wrapping the dimap/first/second/left/right combination
public sealed class ForgetOptInstance<R> : AffineP<ForgetOpts.Mu<R>, ForgetOptInstance<R>.Mu>, App<ForgetOptInstance<R>.Mu, ForgetOpts.Mu<R>>
{
    public sealed class Mu : IAffinePMu { }
    public static readonly ForgetOptInstance<R> InstanceOf = new();
    private ForgetOptInstance() { }

    //dimap preprocesses the input with g, directly calling the original ForgetOpt.run and ignoring h
    public Func<App2<ForgetOpts.Mu<R>, A, B>, App2<ForgetOpts.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.ForgetOpt<R, C, D>(c => ForgetOpts.Unbox<R, A, B>(input).Run(g(c)));

    //first extends to Pair<A,C>, taking the First component and calling the original ForgetOpt.run
    public App2<ForgetOpts.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Pair<A, C>, Pair<B, C>>(p => ForgetOpts.Unbox<R, A, B>(input).Run(p.First));

    //second extends to Pair<C,A>, taking the Second component and calling the original ForgetOpt.run
    public new App2<ForgetOpts.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Pair<C, A>, Pair<C, B>>(p => ForgetOpts.Unbox<R, A, B>(input).Run(p.Second));

    //left extends to Either<A,C>: the left branch flatMaps the original ForgetOpt.run, the right branch returns Empty
    public App2<ForgetOpts.Mu<R>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Either<A, C>, Either<B, C>>(e => e.GetLeft().FlatMap(a => ForgetOpts.Unbox<R, A, B>(input).Run(a)));

    //right extends to Either<C,A>: the right branch flatMaps the original ForgetOpt.run, the left branch returns Empty
    public new App2<ForgetOpts.Mu<R>, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<ForgetOpts.Mu<R>, A, B> input)
        => Optics.ForgetOpt<R, Either<C, A>, Either<C, B>>(e => e.GetRight().FlatMap(a => ForgetOpts.Unbox<R, A, B>(input).Run(a)));
}
