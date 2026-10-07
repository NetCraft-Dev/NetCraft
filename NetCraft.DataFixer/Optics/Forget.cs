namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Forgets container holding the Mu marker, avoiding generic nesting
public static class Forgets
{
    //binary HKT marker; R is the evaluation result type
    public sealed class Mu<R> : K2 { }

    //recover the type application as Forget<R,A,B>
    public static Forget<R, A, B> Unbox<R, A, B>(App2<Mu<R>, A, B> box)
        => (Forget<R, A, B>)(object)box!;
}

//Forget forgetful optic maps to vanilla com.mojang.datafixers.optics.Forget
//evaluator A->R ignoring the B type parameter, used to evaluate read-only optics
public interface Forget<R, A, B> : App2<Forgets.Mu<R>, A, B>
{
    //run takes A and returns R, ignoring B
    R Run(A a);
}

//Forget concrete implementation holding a Func<A,R> delegate
internal sealed class ForgetImpl<R, A, B> : Forget<R, A, B>
{
    private readonly Func<A, R> _function;
    internal ForgetImpl(Func<A, R> function) => _function = function;
    public R Run(A a) => _function(a);
}

//ForgetInstance as the Cartesian+ReCocartesian instance
//uses the forget factory to build a new Forget wrapping the dimap/first/second/unleft/unright combination
public sealed class ForgetInstance<R> : Cartesian<Forgets.Mu<R>, ForgetInstance<R>.Mu>, ReCocartesian<Forgets.Mu<R>, ForgetInstance<R>.Mu>, App<ForgetInstance<R>.Mu, Forgets.Mu<R>>
{
    public sealed class Mu : ICartesianMu, IReCocartesianMu { }
    public static readonly ForgetInstance<R> InstanceOf = new();
    private ForgetInstance() { }

    //dimap inversely maps the input with g (c->a), then calls the original Forget.run to get R
    public Func<App2<Forgets.Mu<R>, A, B>, App2<Forgets.Mu<R>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => Optics.Forget<R, C, D>(c => Forgets.Unbox<R, A, B>(input).Run(g(c)));

    //first extends to Pair<A,C>, taking the First component and calling the original Forget.run
    public App2<Forgets.Mu<R>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Forgets.Mu<R>, A, B> input)
        => Optics.Forget<R, Pair<A, C>, Pair<B, C>>(p => Forgets.Unbox<R, A, B>(input).Run(p.First));

    //second extends to Pair<C,A>, taking the Second component and calling the original Forget.run
    public new App2<Forgets.Mu<R>, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<Forgets.Mu<R>, A, B> input)
        => Optics.Forget<R, Pair<C, A>, Pair<C, B>>(p => Forgets.Unbox<R, A, B>(input).Run(p.Second));

    //unleft takes the left value A from Either<A,C> and calls the original Forget.run
    public App2<Forgets.Mu<R>, A, B> Unleft<A, B, C>(App2<Forgets.Mu<R>, Either<A, C>, Either<B, C>> input)
        => Optics.Forget<R, A, B>(a => Forgets.Unbox<R, Either<A, C>, Either<B, C>>(input).Run(Either<A, C>.Left(a)));

    //unright takes the right value A from Either<C,A> and calls the original Forget.run
    public App2<Forgets.Mu<R>, A, B> Unright<A, B, C>(App2<Forgets.Mu<R>, Either<C, A>, Either<C, B>> input)
        => Optics.Forget<R, A, B>(a => Forgets.Unbox<R, Either<C, A>, Either<C, B>>(input).Run(Either<C, A>.Right(a)));
}
