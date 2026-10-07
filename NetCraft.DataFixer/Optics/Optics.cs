namespace NetCraft.DataFixer.Optics;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//Optics utility class maps to vanilla com.mojang.datafixers.optics.Optics
//provides factory and conversion methods for each optic
public static class Optics
{
    //adapter factory; from/to convert directly
    public static Adapter<S, T, A, B> Adapter<S, T, A, B>(Func<S, A> from, Func<B, T> to)
        => new AdapterImpl<S, T, A, B>(from, to);

    //lens factory; view takes the focus and update replaces it
    public static Lens<S, T, A, B> Lens<S, T, A, B>(Func<S, A> view, Func<B, S, T> update)
        => new LensImpl<S, T, A, B>(view, update);

    //prism factory; match tries to decompose and build constructs T from B
    public static Prism<S, T, A, B> Prism<S, T, A, B>(Func<S, Either<T, A>> match, Func<B, T> build)
        => new PrismImpl<S, T, A, B>(match, build);

    //affine factory; preview tries to take the focus and set replaces it
    public static Affine<S, T, A, B> Affine<S, T, A, B>(Func<S, Either<T, A>> preview, Func<B, S, T> set)
        => new AffineImpl<S, T, A, B>(preview, set);

    //getter factory; get retrieves the value
    public static Getter<S, T, A, B> Getter<S, T, A, B>(Func<S, A> get)
        => new GetterImpl<S, T, A, B>(get);

    //grate factory; the grate function takes a (Func<S,A>)->B function and returns T
    public static Grate<S, T, A, B> Grate<S, T, A, B>(Func<Func<Func<S, A>, B>, T> grate)
        => new GrateImpl<S, T, A, B>(grate);

    //forget factory; evaluator A->R
    public static Forget<R, A, B> Forget<R, A, B>(Func<A, R> function)
        => new ForgetImpl<R, A, B>(function);

    //forgetOpt factory; evaluator A->Optional<R>
    public static ForgetOpt<R, A, B> ForgetOpt<R, A, B>(Func<A, NetCraft.Codec.Optional<R>> function)
        => new ForgetOptImpl<R, A, B>(function);

    //forgetE factory; evaluator A->Either<B,R>
    public static ForgetE<R, A, B> ForgetE<R, A, B>(Func<A, Either<B, R>> function)
        => new ForgetEImpl<R, A, B>(function);

    //reForget factory; evaluator R->B
    public static ReForget<R, A, B> ReForget<R, A, B>(Func<R, B> function)
        => new ReForgetImpl<R, A, B>(function);

    //reForgetE factory; evaluator Either<A,R>->B
    public static ReForgetE<R, A, B> ReForgetE<R, A, B>(string name, Func<Either<A, R>, B> function)
        => new ReForgetEImpl<R, A, B>(name, function);

    //reForgetEP factory; evaluator Either<A,Pair<A,R>>->B
    public static ReForgetEP<R, A, B> ReForgetEP<R, A, B>(string name, Func<Either<A, Pair<A, R>>, B> function)
        => new ReForgetEPImpl<R, A, B>(name, function);

    //reForgetP factory; evaluator (A,R)->B
    public static ReForgetP<R, A, B> ReForgetP<R, A, B>(string name, Func<A, R, B> function)
        => new ReForgetPImpl<R, A, B>(name, function);

    //reForgetC factory; implementation of Either<Func<R,B>,Func<A,R,B>>
    public static ReForgetC<R, A, B> ReForgetC<R, A, B>(string name, Either<Func<R, B>, Func<A, R, B>> impl)
        => new ReForgetCImpl<R, A, B>(name, impl);

    //pStore factory; peek function and pos function
    public static PStore<I, J, X> PStore<I, J, X>(Func<J, X> peek, Func<I> pos)
        => new PStoreImpl<I, J, X>(peek, pos);

    //getFunc recovers FunctionType as Func
    public static Func<A, B> GetFunc<A, B>(App2<FunctionTypes.Mu, A, B> box)
        => FunctionType<A, B>.GetFunc(box);

    //merge combines two Lenses into one Lens, composing view to get a Pair
    //update uses only lens.Update because getter is read-only
    public static Lens<S, T, Pair<F, A>, B> Merge<S, T, A, B, F>(Lens<S, object, F, object> getter, Lens<S, T, A, B> lens)
        => Lens<S, T, Pair<F, A>, B>(
            s => Pair<F, A>.Of(getter.View(s), lens.View(s)),
            lens.Update
        );

    //id identity adapter reuses the IdAdapter singleton; Unsafe.As bypasses the generic invariance check, aligning with Java type erasure
    public static Adapter<S, T, S, T> Id<S, T>() => UnsafeCast<Adapter<S, T, S, T>>(IdAdapter<object, object>.Instance);

    //isId checks whether optic is the IdAdapter singleton
    public static bool IsId(object optic)
        => ReferenceEquals(optic, IdAdapter<object, object>.Instance);

    //proj1 returns the Proj1 singleton; Unsafe.As bypasses the generic invariance check
    //fully-qualified class name avoids conflicting with the Optics.Proj1 method group (CS0119)
    public static Proj1<F, G, F2> Proj1<F, G, F2>() => UnsafeCast<Proj1<F, G, F2>>(global::NetCraft.DataFixer.Optics.Proj1<object, object, object>.Instance);
    //isProj1 checks whether optic is the Proj1 singleton
    public static bool IsProj1(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Proj1<object, object, object>.Instance);

    //proj2 returns the Proj2 singleton; Unsafe.As bypasses the generic invariance check
    public static Proj2<F, G, G2> Proj2<F, G, G2>() => UnsafeCast<Proj2<F, G, G2>>(global::NetCraft.DataFixer.Optics.Proj2<object, object, object>.Instance);
    //isProj2 checks whether optic is the Proj2 singleton
    public static bool IsProj2(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Proj2<object, object, object>.Instance);

    //inj1 returns the Inj1 singleton; Unsafe.As bypasses the generic invariance check
    public static Inj1<F, G, F2> Inj1<F, G, F2>() => UnsafeCast<Inj1<F, G, F2>>(global::NetCraft.DataFixer.Optics.Inj1<object, object, object>.Instance);
    //isInj1 checks whether optic is the Inj1 singleton
    public static bool IsInj1(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Inj1<object, object, object>.Instance);

    //inj2 returns the Inj2 singleton; Unsafe.As bypasses the generic invariance check
    public static Inj2<F, G, G2> Inj2<F, G, G2>() => UnsafeCast<Inj2<F, G, G2>>(global::NetCraft.DataFixer.Optics.Inj2<object, object, object>.Instance);
    //isInj2 checks whether optic is the Inj2 singleton
    public static bool IsInj2(object optic) => ReferenceEquals(optic, global::NetCraft.DataFixer.Optics.Inj2<object, object, object>.Instance);

    //listTraversal returns the ListTraversal singleton; Unsafe.As bypasses the generic invariance check
    public static ListTraversal<A, B> ListTraversal<A, B>() => UnsafeCast<ListTraversal<A, B>>(global::NetCraft.DataFixer.Optics.ListTraversal<object, object>.Instance);

    //UnsafeCast uses Unsafe.As to bypass the C# reference-type generic invariance check, aligning with Java type erasure and singleton sharing
    private static T UnsafeCast<T>(object instance)
        => System.Runtime.CompilerServices.Unsafe.As<object, T>(ref instance);

    //toAdapter converts an optic to Adapter, using AdapterInstance as the Profunctor proof
    public static Adapter<S, T, A, B> ToAdapter<S, T, A, B>(Optic<IProfunctorMu, S, T, A, B> optic)
    {
        var instance = new AdapterInstance<A, B>();
        var func = optic.Eval<Adapters.Mu<A, B>>(instance);
        return Adapters.Unbox<S, T, A, B>(func.Invoke(Adapter<A, B, A, B>(x => x, x => x)));
    }

    //toLens converts an optic to Lens, using LensInstance as the Cartesian proof
    public static Lens<S, T, A, B> ToLens<S, T, A, B>(Optic<ICartesianMu, S, T, A, B> optic)
    {
        var instance = new LensInstance<A, B>();
        var func = optic.Eval<Lenses.Mu<A, B>>(instance);
        return Lenses.Unbox<S, T, A, B>(func.Invoke(Lens<A, B, A, B>(x => x, (b, s) => b)));
    }

    //toPrism converts an optic to Prism, using PrismInstance as the Cocartesian proof
    public static Prism<S, T, A, B> ToPrism<S, T, A, B>(Optic<ICocartesianMu, S, T, A, B> optic)
    {
        var instance = new PrismInstance<A, B>();
        var func = optic.Eval<Prisms.Mu<A, B>>(instance);
        return Prisms.Unbox<S, T, A, B>(func.Invoke(Prism<A, B, A, B>(a => Either<B, A>.Right(a), b => b)));
    }

    //toAffine converts an optic to Affine, using AffineInstance as the AffineP proof
    public static Affine<S, T, A, B> ToAffine<S, T, A, B>(Optic<IAffinePMu, S, T, A, B> optic)
    {
        var instance = new AffineInstance<A, B>();
        var func = optic.Eval<Affines.Mu<A, B>>(instance);
        return Affines.Unbox<S, T, A, B>(func.Invoke(Affine<A, B, A, B>(a => Either<B, A>.Right(a), (b, s) => b)));
    }

    //toGetter converts an optic to Getter, using GetterInstance as the GetterP proof
    public static Getter<S, T, A, B> ToGetter<S, T, A, B>(Optic<IGetterPMu, S, T, A, B> optic)
    {
        var instance = new GetterInstance<A, B>();
        var func = optic.Eval<Getters.Mu<A, B>>(instance);
        return Getters.Unbox<S, T, A, B>(func.Invoke(Getter<A, B, A, B>(x => x)));
    }

    //toTraversal converts an optic to Traversal, using TraversalInstance as the TraversalP proof
    //TraversalInstance is implemented later; earlier toTraversal throws NotSupportedException
    //at runtime optic is a concrete Optic<other Proof> such as Proj2/InjTagged; its virtual method slot does not match Optic<ITraversalPMu,...>
    //calling optic.Eval directly throws EntryPointNotFoundException; use EvalCacheHelper.InvokeEval with reflection + expression-tree compiled delegates instead
    //under strict type checking of the delegate invoke argument, IdAdapter's A/B=object does not match the expected A/B=concrete types
    //uses the FuncInvokerHelper cached reflective Invoke to bypass the delegate invoke type check, aligning with Java type erasure
    public static Traversal<S, T, A, B> ToTraversal<S, T, A, B>(Optic<ITraversalPMu, S, T, A, B> optic)
    {
        var instance = TraversalInstance<A, B>.InstanceOf;
        var funcObj = EvalCacheHelper.InvokeEval<Traversals.Mu<A, B>>((object)optic!, (object)instance!);
        var identity = new IdentityTraversal<A, B>();
        var result = FuncInvokerHelper.Invoke(funcObj!, identity);
        //result is object but actually App2<Mu<A,B>,S,T>; use Unsafe.As to bypass the runtime type check, aligning with Java type erasure
        var resultApp = System.Runtime.CompilerServices.Unsafe.As<object, App2<Traversals.Mu<A, B>, S, T>>(ref result!);
        return Traversals.Unbox<S, T, A, B>(resultApp);
    }

    //eitherLens merges two Lenses into a Lens on Either, handling the left and right values by branch
    public static Lens<Either<F, G>, Either<F2, G2>, A, B> EitherLens<F, G, F2, G2, A, B>(Lens<F, F2, A, B> fLens, Lens<G, G2, A, B> gLens)
        => Lens<Either<F, G>, Either<F2, G2>, A, B>(
            either => either.Map(f => fLens.View(f), g => gLens.View(g)),
            (b, either) => either.MapBoth(f => fLens.Update(b, f), g => gLens.Update(b, g))
        );

    //eitherAffine merges two Affines into an Affine on Either, handling the left and right values by branch
    public static Affine<Either<F, G>, Either<F2, G2>, A, B> EitherAffine<F, G, F2, G2, A, B>(Affine<F, F2, A, B> fAffine, Affine<G, G2, A, B> gAffine)
        => Affine<Either<F, G>, Either<F2, G2>, A, B>(
            either => either.Map(
                f => fAffine.Preview(f).MapLeft(Either<F2, G2>.Left),
                g => gAffine.Preview(g).MapLeft(Either<F2, G2>.Right)
            ),
            (b, either) => either.MapBoth(f => fAffine.Set(b, f), g => gAffine.Set(b, g))
        );

    //eitherTraversal merges two Traversals into a Traversal on Either, handling the left and right values by branch
    public static Traversal<Either<F, G>, Either<F2, G2>, A, B> EitherTraversal<F, G, F2, G2, A, B>(Traversal<F, F2, A, B> fOptic, Traversal<G, G2, A, B> gOptic)
        => new EitherTraversalImpl<F, G, F2, G2, A, B>(fOptic, gOptic);
}

//IdentityTraversal identity Traversal used as the seed of ToTraversal; wander returns the original function directly
internal sealed class IdentityTraversal<A, B> : Traversal<A, B, A, B>
{
    public Func<A, App<F, B>> Wander<F, TMu2>(Applicative<F, TMu2> applicative, Func<A, App<F, B>> input) where F : K1 where TMu2 : IApplicativeMu
        => input;
}

//EitherTraversal merges two Traversals into Either branches; wander delegates by branch
internal sealed class EitherTraversalImpl<F, G, F2, G2, A, B> : Traversal<Either<F, G>, Either<F2, G2>, A, B>
{
    private readonly Traversal<F, F2, A, B> _fOptic;
    private readonly Traversal<G, G2, A, B> _gOptic;
    internal EitherTraversalImpl(Traversal<F, F2, A, B> fOptic, Traversal<G, G2, A, B> gOptic)
    {
        _fOptic = fOptic;
        _gOptic = gOptic;
    }

    public Func<Either<F, G>, App<FT, Either<F2, G2>>> Wander<FT, TMu2>(Applicative<FT, TMu2> applicative, Func<A, App<FT, B>> input) where FT : K1 where TMu2 : IApplicativeMu
        => e => e.Map(
            l => applicative.Ap(Either<F2, G2>.Left, _fOptic.Wander(applicative, input).Invoke(l)),
            r => applicative.Ap(Either<F2, G2>.Right, _gOptic.Wander(applicative, input).Invoke(r))
        );
}
