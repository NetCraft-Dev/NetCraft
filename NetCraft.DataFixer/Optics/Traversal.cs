namespace NetCraft.DataFixer.Optics;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Traversal container holding the Mu marker, avoiding generic nesting
public static class Traversals
{
    //binary HKT marker; A/B are the focus/new value types
    public sealed class Mu<A, B> : K2 { }

    //recover the type application as Traversal<S,T,A,B>
    //at runtime box is a concrete type such as DimapTraversal<...> inheriting Traversal<concrete S,T,A,B>
    //castclass fails across generic instantiations; use Unsafe.As to bypass it, aligning with Java type erasure semantics
    public static Traversal<S, T, A, B> Unbox<S, T, A, B>(App2<Mu<A, B>, S, T> box)
    {
        var obj = (object)box!;
        return System.Runtime.CompilerServices.Unsafe.As<object, Traversal<S, T, A, B>>(ref obj);
    }
}

//Traversal traversal optic maps to vanilla com.mojang.datafixers.optics.Traversal
//inherits the Wander strategy; based on TraversalP to traverse any Applicative container
public interface Traversal<S, T, A, B> : Wander<S, T, A, B>, App2<Traversals.Mu<A, B>, S, T>, Optic<ITraversalPMu, S, T, A, B>
{
    //eval uses TraversalP.wander to apply the Wander strategy to input, extending A->B to S->T
    Func<App2<P, A, B>, App2<P, S, T>> Optic<ITraversalPMu, S, T, A, B>.Eval<P>(App<ITraversalPMu, P> proof)
    {
        var traversalP = TraversalP<P, ITraversalPMu>.Unbox(proof);
        return input => traversalP.Wander<S, T, A, B>(this, input);
    }
}

//TraversalInstance as the TraversalP instance, maps to vanilla Traversal.Instance
//TMu uses ITraversalPMu directly so the instance can be passed to Optic.Eval as App<ITraversalPMu,Traversals.Mu<A2,B2>>
//First/Left use TraversalP interface defaults based on Traverse+Dimap; only Dimap+Wander need implementing
//additionally implements the ICartesianMu/ICocartesianMu/IAffinePMu/ITraversalPMu variant interfaces of Cartesian/Cocartesian/Profunctor
//lets calls such as Lens.Eval reach the Dimap/First method table entry via a Cartesian<Traversals.Mu,ICartesianMu> reference, aligning with Java type erasure
public sealed class TraversalInstance<A2, B2> :
    TraversalP<Traversals.Mu<A2, B2>, ITraversalPMu>,
    App<ITraversalPMu, Traversals.Mu<A2, B2>>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IProfunctorMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ICocartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICocartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.AffineP<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ITraversalPMu>,
    App<ICartesianMu, Traversals.Mu<A2, B2>>,
    App<ICocartesianMu, Traversals.Mu<A2, B2>>,
    App<IAffinePMu, Traversals.Mu<A2, B2>>,
    App<IProfunctorMu, Traversals.Mu<A2, B2>>
{
    public static readonly TraversalInstance<A2, B2> InstanceOf = new();
    private TraversalInstance() { }

    //dimap wraps inner's wander with DimapTraversal, preprocessing the input with g and postprocessing the output with h
    public Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => input => new DimapTraversal<A2, B2, A, B, C, D>(g, h, Traversals.Unbox<A, B, A2, B2>(input));

    //wander uses WanderTraversal to combine the outer wander with inner's wander
    public App2<Traversals.Mu<A2, B2>, S, T> Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<Traversals.Mu<A2, B2>, A, B> input)
        => new WanderTraversal<A2, B2, S, T, A, B>(wander, Traversals.Unbox<A, B, A2, B2>(input));

    //first extends to the first component of Pair via Pair's Traversable, based on Wander+TraverseWander+Dimap
    //explicit implementation because C# interface default methods do not automatically satisfy the Cartesian abstract methods
    public App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
    {
        var traversed = Wander<App<Pairs.Mu<C>, A>, App<Pairs.Mu<C>, B>, A, B>(
            new TraverseWander<Pairs.Mu<C>, PairInstance<C>.Mu, A, B>(new PairInstance<C>()), input);
        return Dimap<App<Pairs.Mu<C>, A>, App<Pairs.Mu<C>, B>, Pair<A, C>, Pair<B, C>>(
            pair => (App<Pairs.Mu<C>, A>)(object)pair!,
            app => Pair<B, C>.Unbox(app)
        ).Invoke(traversed);
    }

    //left extends to the either left branch via Either's Traversable, based on Wander+TraverseWander+Dimap
    public App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
    {
        var traversed = Wander<App<Eithers.Mu<C>, A>, App<Eithers.Mu<C>, B>, A, B>(
            new TraverseWander<Eithers.Mu<C>, EitherInstance<C>.Mu, A, B>(new EitherInstance<C>()), input);
        return Dimap<App<Eithers.Mu<C>, A>, App<Eithers.Mu<C>, B>, Either<A, C>, Either<B, C>>(
            either => (App<Eithers.Mu<C>, A>)(object)either!,
            app => Either<B, C>.Unbox(app)
        ).Invoke(traversed);
    }

    //explicitly implements Profunctor<Traversals.Mu<A2,B2>,IProfunctorMu>.Dimap so calls through that interface reference reach the method table entry
    //aligns with Java virtual dispatch semantics after type erasure
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IProfunctorMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Profunctor<Traversals.Mu<A2,B2>,ICartesianMu>.Dimap so cartesian.Dimap in Lens.Eval reaches the entry
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Profunctor<Traversals.Mu<A2,B2>,ICocartesianMu>.Dimap
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ICocartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Profunctor<Traversals.Mu<A2,B2>,IAffinePMu>.Dimap
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, IAffinePMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Profunctor<Traversals.Mu<A2,B2>,ITraversalPMu>.Dimap
    Func<App2<Traversals.Mu<A2, B2>, A, B>, App2<Traversals.Mu<A2, B2>, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<Traversals.Mu<A2, B2>, ITraversalPMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Cartesian<Traversals.Mu<A2,B2>,ICartesianMu>.First so cartesian.First in Lens.Eval reaches the entry
    App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ICartesianMu>.First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => First<A, B, C>(input);

    //explicitly implements Cartesian<Traversals.Mu<A2,B2>,IAffinePMu>.First
    App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, IAffinePMu>.First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => First<A, B, C>(input);

    //explicitly implements Cartesian<Traversals.Mu<A2,B2>,ITraversalPMu>.First
    App2<Traversals.Mu<A2, B2>, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<Traversals.Mu<A2, B2>, ITraversalPMu>.First<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => First<A, B, C>(input);

    //explicitly implements Cocartesian<Traversals.Mu<A2,B2>,ICocartesianMu>.Left
    App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ICocartesianMu>.Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => Left<A, B, C>(input);

    //explicitly implements Cocartesian<Traversals.Mu<A2,B2>,IAffinePMu>.Left
    App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, IAffinePMu>.Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => Left<A, B, C>(input);

    //explicitly implements Cocartesian<Traversals.Mu<A2,B2>,ITraversalPMu>.Left
    App2<Traversals.Mu<A2, B2>, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<Traversals.Mu<A2, B2>, ITraversalPMu>.Left<A, B, C>(App2<Traversals.Mu<A2, B2>, A, B> input)
        => Left<A, B, C>(input);
}

//DimapTraversal preprocesses the input with g and postprocesses the output with h, delegating to inner's wander
//maps to the anonymous Traversal implementation in vanilla Traversal.Instance.dimap
internal sealed class DimapTraversal<A2, B2, A, B, C, D> : Traversal<C, D, A2, B2>
{
    private readonly Func<C, A> _g;
    private readonly Func<B, D> _h;
    private readonly Traversal<A, B, A2, B2> _inner;
    internal DimapTraversal(Func<C, A> g, Func<B, D> h, Traversal<A, B, A2, B2> inner)
    {
        _g = g;
        _h = h;
        _inner = inner;
    }
    public Func<C, App<F, D>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A2, App<F, B2>> input) where F : K1 where TMu2 : IApplicativeMu
        => c =>
        {
            //at runtime _inner is Traversal<concrete A,B,A2,B2> and is Unsafe.As-cast to Traversal<A,B,A2,B2>
            //under C# strict generic invariance the two closed types do not share a method table entry; calling Wander directly throws EntryPointNotFoundException
            //uses WanderInvokerCache.GetWanderFunc, a reflective delegate cache, to call Wander, aligning with Java virtual dispatch semantics after type erasure
            var wanderFuncObj = WanderInvokerCache.GetWanderFunc<A2, B2, F, TMu2>((object)_inner!, (object)applicative!, input);
            var resultObj = WanderInvokerCache.InvokeWanderFunc(wanderFuncObj, (object)_g(c)!);
            var resultApp = System.Runtime.CompilerServices.Unsafe.As<object, App<F, B>>(ref resultObj!);
            return applicative.Map<B, D>(_h, resultApp);
        };
}

//WanderTraversal combines the outer wander with inner's wander
//maps to the anonymous Traversal implementation in vanilla Traversal.Instance.wander
internal sealed class WanderTraversal<A2, B2, S, T, A, B> : Traversal<S, T, A2, B2>
{
    private readonly Wander<S, T, A, B> _wander;
    private readonly Traversal<A, B, A2, B2> _inner;
    internal WanderTraversal(Wander<S, T, A, B> wander, Traversal<A, B, A2, B2> inner)
    {
        _wander = wander;
        _inner = inner;
    }
    public Func<S, App<F, T>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A2, App<F, B2>> function) where F : K1 where TMu2 : IApplicativeMu
    {
        //at runtime _inner is Traversal<concrete A,B,A2,B2> and is Unsafe.As-cast to Traversal<A,B,A2,B2>
        //under C# strict generic invariance the two closed types do not share a method table entry; calling Wander directly throws EntryPointNotFoundException
        //uses WanderInvokerCache.GetWanderFunc, a reflective delegate cache, to call Wander, aligning with Java virtual dispatch semantics after type erasure
        var innerWanderFuncObj = WanderInvokerCache.GetWanderFunc<A2, B2, F, TMu2>((object)_inner!, (object)applicative!, function);
        //GetWanderFunc returns object, actually Func<A,App<F,B>>; use Unsafe.As to cast back to the strong type, aligning with Java type erasure semantics
        var innerWanderFunc = System.Runtime.CompilerServices.Unsafe.As<object, Func<A, App<F, B>>>(ref innerWanderFuncObj!);
        //at runtime _wander is a Wander<concrete S,T,A,B> interface instance; calling Wander directly throws EntryPointNotFoundException
        //uses the GetWanderFuncForWander delegate cache to bypass the method table entry check, aligning with Java virtual dispatch semantics after type erasure
        return WanderInvokerCache.GetWanderFuncForWander<S, T, F, TMu2, A, B>((object)_wander!, (object)applicative!, innerWanderFunc);
    }
}
