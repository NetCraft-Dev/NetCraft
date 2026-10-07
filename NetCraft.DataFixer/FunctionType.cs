namespace NetCraft.DataFixer;

using System;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Util;

//FunctionTypes container holding Mu and ReaderMu markers, avoiding generic nesting
public static class FunctionTypes
{
    //binary HKT marker; function type constructor
    public sealed class Mu : K2 { }

    //unary Reader HKT marker; R is the environment type
    public sealed class ReaderMu<R> : K1 { }
}

//function type maps to vanilla com.mojang.datafixers.FunctionType
//wraps Func<A,B> as an HKT so it can be treated as a profunctor
public interface FunctionType<A, B> : App2<FunctionTypes.Mu, A, B>, App<FunctionTypes.ReaderMu<A>, B>
{
    //applies the function and returns B
    B Apply(A a);

    //recover the binary type application as FunctionType
    //use Unsafe.As to bypass the runtime type check and align with Java type erasure
    //the actual instance may be FunctionTypeImpl<object,object>; casting to FunctionType<A2,B2> requires bypassing C# strict generic invariance
    static FunctionType<A2, B2> Unbox<A2, B2>(App2<FunctionTypes.Mu, A2, B2> box)
    {
        var boxObj = (object)box!;
        return System.Runtime.CompilerServices.Unsafe.As<object, FunctionType<A2, B2>>(ref boxObj);
    }

    //recover the unary Reader type application as FunctionType
    //use Unsafe.As to bypass the runtime type check and align with Java type erasure
    static FunctionType<A2, B2> UnboxReader<A2, B2>(App<FunctionTypes.ReaderMu<A2>, B2> box)
    {
        var boxObj = (object)box!;
        return System.Runtime.CompilerServices.Unsafe.As<object, FunctionType<A2, B2>>(ref boxObj);
    }

    //factory method building a FunctionType from a Func
    static FunctionType<A, B> Create(Func<A, B> function) => new FunctionTypeImpl<A, B>(function);

    //recovers to Func
    //at runtime box may be FunctionTypeImpl<X,Y> but its compile-time declaration is App2<Mu,A,B>
    //X/Y and A/B differ under Java type erasure semantics but are the same instance at runtime
    //calling Unbox(box).Apply directly makes CLR interface dispatch look up by the instance type FunctionTypeImpl<X,Y>
    //the FunctionType<A,B>.Apply entry fails with EntryPointNotFoundException
    //delegate variance also disallows casting Func<X,Y> to Func<A,B> due to strict parameter contravariance checks
    //compiles an invocation delegate with an expression tree, cached by funcType, aligning with Java type erasure
    static Func<A, B> GetFunc<A, B>(App2<FunctionTypes.Mu, A, B> box)
    {
        var boxObj = (object)box!;
        var boxType = boxObj.GetType();
        if (boxType.IsGenericType && boxType.GetGenericTypeDefinition() == typeof(FunctionTypeImpl<,>))
        {
            var fieldInfo = FunctionTypeImplFieldCache.GetField(boxType);
            var funcObj = fieldInfo.GetValue(boxObj)!;
            var invoker = FunctionTypeInvokerCache.GetInvoker(funcObj.GetType());
            //the invoker returns funcObj whose actual return type, such as Pair<string,object>, differs from B=Pair<object,object>
            //under C# strict generic invariance Pair<string,object> cannot be cast to Pair<object,object>
            //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            return a =>
            {
                var result = invoker(funcObj, a!);
                var obj = (object)result;
                return System.Runtime.CompilerServices.Unsafe.As<object, B>(ref obj);
            };
        }
        return Unbox(box).Apply;
    }
}

//FunctionTypeInvoker caches the compiled invocation delegate by funcType
//the expression tree compiles Func<X,Y>.Invoke(object,object)->object into a strongly-typed delegate
//converting argParam to the target parameter type uses CastTo<T> wrapping Unsafe.As to bypass the castclass runtime check
//otherwise Pair<string,object> cannot castclass to Pair<object,object>, aligning with C# strict generic invariance
internal static class FunctionTypeInvokerCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Func<object, object, object>> _cache = new();

    public static System.Func<object, object, object> GetInvoker(Type funcType)
        => _cache.GetOrAdd(funcType, t =>
        {
            var invokeMethod = t.GetMethod("Invoke")!;
            var paramType = invokeMethod.GetParameters()[0].ParameterType;
            var funcParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "func");
            var argParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "arg");
            //CastTo<object,Pair<string,object>>(arg) uses Unsafe.As to bypass castclass
            var castMethod = typeof(FunctionTypeInvokerCache)
                .GetMethod(nameof(CastTo), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(paramType);
            var castArg = System.Linq.Expressions.Expression.Call(castMethod, argParam);
            var call = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Convert(funcParam, t),
                invokeMethod,
                castArg);
            return System.Linq.Expressions.Expression.Lambda<System.Func<object, object, object>>(
                System.Linq.Expressions.Expression.Convert(call, typeof(object)),
                funcParam, argParam).Compile();
        });

    //CastTo uses Unsafe.As to bypass C# strict generic invariance, converting object to any type T
    //removing the class constraint lets struct-typed parameters such as Pair<object,object> also use MakeGenericMethod
    //aligns with Java type erasure semantics, avoiding castclass runtime check failure
    private static T CastTo<T>(object obj)
    {
        var local = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
    }
}

//FunctionTypeImpl field reflection cache, avoiding a reflective lookup in GetFunc each time
//caches _functionFieldInfo by runtime type, keyed by boxType
internal static class FunctionTypeImplFieldCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo> _cache = new();

    public static System.Reflection.FieldInfo GetField(Type implType)
        => _cache.GetOrAdd(implType, t => t.GetField("_function", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!);
}

//FunctionType concrete implementation holding a Func delegate
internal sealed class FunctionTypeImpl<A, B> : FunctionType<A, B>
{
    private readonly Func<A, B> _function;
    internal FunctionTypeImpl(Func<A, B> function) => _function = function;
    public B Apply(A a) => _function(a);
}

//FunctionTypeInstance as an instance of TraversalP+Monoidal+Mapping+MonoidProfunctor
//all methods are implemented via Func composition
//additionally implements App<IProfunctorMu,FunctionTypes.Mu>, aligning with Java type erasure semantics
//when ProfunctorTransformer.Eval reflectively calls IdAdapter.Eval, the proof parameter has type App<Proof,P>
//under C# strict generic invariance App<FunctionTypeInstance.Mu,P> and App<IProfunctorMu,P> are different closed types
//under Java type erasure Proof is erased and equals App<Object,Object> at runtime, so any App instance works
//implementing the App<IProfunctorMu,P> interface lets the reflective type check pass, aligning with Java virtual dispatch semantics
//additionally implements the Profunctor<FunctionTypes.Mu,IProfunctorMu> interface so the reference returned by Profunctor.Unbox
//can virtually dispatch to Dimap, reaching the Profunctor<FunctionTypes.Mu,IProfunctorMu> method table entry
//additionally implements the Cartesian<FunctionTypes.Mu,ICartesianMu> interface so the reference returned by Cartesian.Unbox
//can call Dimap through the Cartesian<FunctionTypes.Mu,ICartesianMu> method table and hit the entry, aligning with Java type erasure
public sealed class FunctionTypeInstance :
    TraversalP<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    Monoidal<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    Mapping<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    MonoidProfunctor<FunctionTypes.Mu, FunctionTypeInstance.Mu>,
    App<FunctionTypeInstance.Mu, FunctionTypes.Mu>,
    App<IProfunctorMu, FunctionTypes.Mu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, IProfunctorMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ICocartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICartesianMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICocartesianMu>,
    //additionally implements the ITraversalPMu variant so Traversal.Eval's call to traversalP.Wander hits the method table entry
    //after TraversalP.Unbox uses Unsafe.As to bypass the TMu check and calls Wander, the variant interface method table must exist
    NetCraft.DataFixer.Optics.Profunctors.TraversalP<FunctionTypes.Mu, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.AffineP<FunctionTypes.Mu, IAffinePMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ITraversalPMu>,
    NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ITraversalPMu>,
    App<ITraversalPMu, FunctionTypes.Mu>,
    App<IAffinePMu, FunctionTypes.Mu>
{
    public sealed class Mu : ITraversalPMu, IMonoidalMu, IMappingMu, IMonoidProfunctorMu { }
    public static readonly FunctionTypeInstance InstanceOf = new();
    private FunctionTypeInstance() { }

    //dimap preprocesses the input with g and postprocesses the output with h, composing the original function
    public Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => f => FunctionType<C, D>.Create(a => h(FunctionType<A, B>.GetFunc(f)(g(a))));

    //explicitly implements Profunctor<FunctionTypes.Mu,IProfunctorMu>.Dimap, delegating to the original Dimap
    //so calling Dimap through a Profunctor<P,IProfunctorMu> reference hits the method table entry
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, IProfunctorMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Profunctor<FunctionTypes.Mu,ICartesianMu>.Dimap so the reference returned by Cartesian.Unbox
    //can call Dimap through the Cartesian<FunctionTypes.Mu,ICartesianMu> method table and hit the entry, aligning with Java type erasure
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Profunctor<FunctionTypes.Mu,ICocartesianMu>.Dimap
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ICocartesianMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Cartesian<FunctionTypes.Mu,ICartesianMu>.First so cartesian.First in Lens.Eval hits the entry
    App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ICartesianMu>.First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => First<A, B, C>(input);

    //explicitly implements Cartesian<FunctionTypes.Mu,ICartesianMu>.Second so a Cartesian.Unbox reference call to Second hits the entry
    App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ICartesianMu>.Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Second<A, B, C>(input);

    //explicitly implements Cocartesian<FunctionTypes.Mu,ICocartesianMu>.Left so cocartesian.Left in Prism.Eval hits the entry
    App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ICocartesianMu>.Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Left<A, B, C>(input);

    //explicitly implements Cocartesian<FunctionTypes.Mu,ICocartesianMu>.Right
    App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ICocartesianMu>.Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Right<A, B, C>(input);

    //explicitly implements TraversalP<FunctionTypes.Mu,ITraversalPMu>.Wander so Traversal.Eval's call to traversalP.Wander hits the entry
    App2<FunctionTypes.Mu, S, T> NetCraft.DataFixer.Optics.Profunctors.TraversalP<FunctionTypes.Mu, ITraversalPMu>.Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<FunctionTypes.Mu, A, B> input)
        => Wander<S, T, A, B>(wander, input);

    //explicitly implements Profunctor<FunctionTypes.Mu,ITraversalPMu>.Dimap, delegating to the original Dimap
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, ITraversalPMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Cartesian<FunctionTypes.Mu,ITraversalPMu>.First
    App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ITraversalPMu>.First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => First<A, B, C>(input);

    //explicitly implements Cartesian<FunctionTypes.Mu,ITraversalPMu>.Second
    App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, ITraversalPMu>.Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Second<A, B, C>(input);

    //explicitly implements Cocartesian<FunctionTypes.Mu,ITraversalPMu>.Left
    App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ITraversalPMu>.Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Left<A, B, C>(input);

    //explicitly implements Cocartesian<FunctionTypes.Mu,ITraversalPMu>.Right
    App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, ITraversalPMu>.Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Right<A, B, C>(input);

    //explicitly implements Profunctor<FunctionTypes.Mu,IAffinePMu>.Dimap
    Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, C, D>> NetCraft.DataFixer.Optics.Profunctors.Profunctor<FunctionTypes.Mu, IAffinePMu>.Dimap<A, B, C, D>(Func<C, A> g, Func<B, D> h)
        => Dimap<A, B, C, D>(g, h);

    //explicitly implements Cartesian<FunctionTypes.Mu,IAffinePMu>.First
    App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, IAffinePMu>.First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => First<A, B, C>(input);

    //explicitly implements Cartesian<FunctionTypes.Mu,IAffinePMu>.Second
    App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cartesian<FunctionTypes.Mu, IAffinePMu>.Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Second<A, B, C>(input);

    //explicitly implements Cocartesian<FunctionTypes.Mu,IAffinePMu>.Left
    App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, IAffinePMu>.Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Left<A, B, C>(input);

    //explicitly implements Cocartesian<FunctionTypes.Mu,IAffinePMu>.Right
    App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> NetCraft.DataFixer.Optics.Profunctors.Cocartesian<FunctionTypes.Mu, IAffinePMu>.Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => Right<A, B, C>(input);

    //first extends A->B to Pair<A,C>->Pair<B,C>, preserving the C component
    public App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, C>> First<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => FunctionType<Pair<A, C>, Pair<B, C>>.Create(p => Pair<B, C>.Of(FunctionType<A, B>.GetFunc(input)(p.First), p.Second));

    //second extends A->B to Pair<C,A>->Pair<C,B>, preserving the C component
    public new App2<FunctionTypes.Mu, Pair<C, A>, Pair<C, B>> Second<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
        => FunctionType<Pair<C, A>, Pair<C, B>>.Create(p => Pair<C, B>.Of(p.First, FunctionType<A, B>.GetFunc(input)(p.Second)));

    //left extends A->B to Either<A,C>->Either<B,C>, preserving the C branch
    public App2<FunctionTypes.Mu, Either<A, C>, Either<B, C>> Left<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<Either<A, C>, Either<B, C>>.Create(e => e.MapLeft(func));
    }

    //right extends A->B to Either<C,A>->Either<C,B>, preserving the C branch
    public new App2<FunctionTypes.Mu, Either<C, A>, Either<C, B>> Right<A, B, C>(App2<FunctionTypes.Mu, A, B> input)
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<Either<C, A>, Either<C, B>>.Create(e => e.MapRight(func));
    }

    //par combines two functions in parallel, handling the two Pair components separately
    public App2<FunctionTypes.Mu, Pair<A, C>, Pair<B, D>> Par<A, B, C, D>(App2<FunctionTypes.Mu, A, B> first, Func<App2<FunctionTypes.Mu, C, D>> second)
        => FunctionType<Pair<A, C>, Pair<B, D>>.Create(p => Pair<B, D>.Of(FunctionType<A, B>.GetFunc(first)(p.First), FunctionType<C, D>.GetFunc(second())(p.Second)));

    //empty returns the Void->Void identity element
    public App2<FunctionTypes.Mu, Unit, Unit> Empty()
        => FunctionType<Unit, Unit>.Create(u => u);

    //wander uses IdF as the Applicative to extend A->B to S->T, based on the Wander strategy
    public App2<FunctionTypes.Mu, S, T> Wander<S, T, A, B>(Wander<S, T, A, B> wander, App2<FunctionTypes.Mu, A, B> input)
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<S, T>.Create(s => IdFs.Get(wander.Wander(IdFInstance.InstanceOf, a => IdFs.Create(func(a))).Invoke(s)));
    }

    //mapping uses Functor.map to lift A->B to App<F,A>->App<F,B>
    public App2<FunctionTypes.Mu, App<F, A>, App<F, B>> Mapping<A, B, F, TMu2>(Functor<F, TMu2> functor, App2<FunctionTypes.Mu, A, B> input)
        where F : K1 where TMu2 : IFunctorMu
    {
        var func = FunctionType<A, B>.GetFunc(input);
        return FunctionType<App<F, A>, App<F, B>>.Create(fa => functor.Map(func, fa));
    }

    //zero returns func itself as the identity element
    public App2<FunctionTypes.Mu, A, B> Zero<A, B>(App2<FunctionTypes.Mu, A, B> func) => func;

    //plus composes first (A->C) and second (C->B) with Procompose to get A->B
    public App2<FunctionTypes.Mu, A, B> Plus<A, B>(App2<Procomposes.Mu<FunctionTypes.Mu, FunctionTypes.Mu>, A, B> input)
    {
        var cmp = Procompose<FunctionTypes.Mu, FunctionTypes.Mu, A, B, object>.Unbox(input);
        var firstFunc = FunctionType<A, object>.GetFunc(cmp.First().Invoke());
        var secondFunc = FunctionType<object, B>.GetFunc(cmp.Second());
        return FunctionType<A, B>.Create(a => secondFunc(firstFunc(a)));
    }
}
