namespace NetCraft.DataFixer.Optics.Profunctors;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Util;

//TraversalP traversal profunctor maps to vanilla com.mojang.datafixers.optics.profunctors.TraversalP
//aggregates AffineP and extends Wander; Traversal is based on this
//provides a default traverse based on wander, and default First/Left based on traverse+dimap
public interface TraversalP<P, TMu> : AffineP<P, TMu> where P : K2 where TMu : ITraversalPMu
{
    //recover the type application as TraversalP
    //FunctionTypeInstance only implements TraversalP<FunctionTypes.Mu,FunctionTypeInstance.Mu>
    //the caller passes ITraversalPMu as TMu2, so the cast fails; use Unsafe.As to bypass the runtime type check, aligning with Java type erasure
    static TraversalP<P2, TMu2> Unbox<P2, TMu2>(App<TMu2, P2> proofBox) where P2 : K2 where TMu2 : ITraversalPMu
    {
        var obj = (object)proofBox;
        return System.Runtime.CompilerServices.Unsafe.As<object, TraversalP<P2, TMu2>>(ref obj);
    }

    //wander uses the Wander strategy to extend A->B to S->T
    App2<P, S, T> Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<P, A, B> input);

    //traverse uses Traversable to extend A->B to App<T,A>->App<T,B>, based on wander + an anonymous Wander strategy
    //TMu3 explicitly declares the Traversable marker type because C# has no Java wildcards
    App2<P, App<T, A>, App<T, B>> Traverse<T, TMu3, A, B>(Traversable<T, TMu3> traversable, App2<P, A, B> input) where T : K1 where TMu3 : ITraversableMu
        => Wander<App<T, A>, App<T, B>, A, B>(new TraverseWander<T, TMu3, A, B>(traversable), input);

    //first extends to the first component of Pair via Pair's Traversable, based on traverse + identity dimap
    public App2<P, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<P, A, B> input)
        => Dimap<App<Pairs.Mu<C>, A>, App<Pairs.Mu<C>, B>, Pair<A, C>, Pair<B, C>>(
            Traverse(new PairInstance<C>(), input),
            pair => (App<Pairs.Mu<C>, A>)(object)pair!,
            app => Pair<B, C>.Unbox(app)
        );

    //left extends to the either left branch via Either's Traversable, based on traverse + identity dimap
    public App2<P, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<P, A, B> input)
        => Dimap<App<Eithers.Mu<C>, A>, App<Eithers.Mu<C>, B>, Either<A, C>, Either<B, C>>(
            Traverse(new EitherInstance<C>(), input),
            either => (App<Eithers.Mu<C>, A>)(object)either!,
            app => Either<B, C>.Unbox(app)
        );
}

//the Wander strategy implementation used by traverse holds a Traversable delegate, processing App<T,A> via traversable.traverse
internal sealed class TraverseWander<T, TMu3, A, B> : Wander<App<T, A>, App<T, B>, A, B> where T : K1 where TMu3 : ITraversableMu
{
    private readonly Traversable<T, TMu3> _traversable;
    internal TraverseWander(Traversable<T, TMu3> traversable) => _traversable = traversable;

    public Func<App<T, A>, App<F, App<T, B>>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> input) where F : K1 where TMu2 : IApplicativeMu
        => ta => _traversable.Traverse(applicative, input, ta);
}
