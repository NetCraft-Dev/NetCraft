namespace NetCraft.DataFixer.Optics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetCraft.DataFixer.Kinds;

//Optic interface core combinator, maps to vanilla com.mojang.datafixers.optics.Optic
//Proof is the proof-type constraint, the weakest acceptable profunctor
//S/T are the source/target types; A/B are the focus/new value
public interface Optic<Proof, S, T, A, B> where Proof : K1
{
    //eval receives a profunctor proof and returns a function App2<P,A,B>->App2<P,S,T>
    Func<App2<P, A, B>, App2<P, S, T>> Eval<P>(App<Proof, P> proof) where P : K2;
}

//composite optic maps to vanilla Optic.CompositionOptic
//holds a set of optics that chain functions from right to left
public sealed record CompositionOptic<Proof, S, T, A, B>(IReadOnlyList<object> Optics) : Optic<Proof, S, T, A, B> where Proof : K1
{
    //eval collects each optic's eval function from right to left, then applies them in a chain
    //uses a compiled-delegate cache via expression trees to avoid reflective Invoke each time
    public Func<App2<P, A, B>, App2<P, S, T>> Eval<P>(App<Proof, P> proof) where P : K2
    {
        //stores func in object[] to avoid the internal array covariance check of List<Func<...>> throwing ArrayTypeMismatchException
        //func's runtime type carries concrete generic parameters and has no inheritance relation to Func<App2<P,object,object>,App2<P,object,object>>
        var functions = new List<object>();
        for (int i = Optics.Count - 1; i >= 0; i--)
        {
            var optic = Optics[i];
            var func = EvalCacheHelper.InvokeEval<P>(optic, proof!);
            functions.Add(func!);
        }
        return input =>
        {
            var inputObj = (object)input;
            var inputCast = System.Runtime.CompilerServices.Unsafe.As<object, App2<P, object, object>>(ref inputObj);
            App2<P, object, object> result = inputCast;
            foreach (var function in functions)
            {
                var funcObj = function;
                var funcCast = System.Runtime.CompilerServices.Unsafe.As<object, Func<App2<P, object, object>, App2<P, object, object>>>(ref funcObj);
                result = funcCast(result);
            }
            var resultObj = (object)result;
            return System.Runtime.CompilerServices.Unsafe.As<object, App2<P, S, T>>(ref resultObj);
        };
    }

    public override string ToString()
    {
        return "(" + string.Join(" \u25E6 ", Optics.Select(o => o!.ToString())) + ")";
    }
}

//EvalCacheHelper shares the expression-tree cache across Optics, avoiding repeated reflective Invoke
//maps to vanilla Java virtual dispatch after type erasure; C# emulates it with compiled delegates
internal static class EvalCacheHelper
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type opticType, Type pType), System.Func<object, object, object>> _evalCache = new();

    //ForceCast uses Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
    //at runtime proof is FunctionTypeInstance, implementing the App<FunctionTypeInstance.Mu,P> interface
    //but Eval expects different closed generic types such as App<ICartesianMu,P>; the cast fails under C# strict generic invariance
    private static T ForceCast<T>(object obj)
    {
        var o = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref o);
    }

    public static object InvokeEval<P>(object optic, object proofInstance)
    {
        var opticType = optic.GetType();
        var pType = typeof(P);
        var func = _evalCache.GetOrAdd((opticType, pType), key =>
        {
            //Eval may be an explicit interface implementation whose name carries the interface prefix, so GetMethod("Eval") fails
            //iterates all interfaces to find the generic method named Eval, aligning with Java virtual dispatch semantics
            MethodInfo? method = FindEvalMethod(key.opticType);
            if (method == null)
            {
                throw new InvalidOperationException($"Eval method not found on {key.opticType.FullName}");
            }
            method = method.MakeGenericMethod(key.pType);
            var proofType = method.GetParameters()[0].ParameterType;
            //expression-tree compiled (o,p)=>optic.Eval<P>(ForceCast<App<Proof,P>>(p))
            //ForceCast uses Unsafe.As to bypass the proof parameter's runtime type check, aligning with Java type erasure semantics
            var opticParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            var proofParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "p");
            var forceCastMethod = typeof(EvalCacheHelper).GetMethod("ForceCast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.MakeGenericMethod(proofType);
            var call = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Convert(opticParam, key.opticType),
                method,
                System.Linq.Expressions.Expression.Call(forceCastMethod, proofParam));
            var lambda = System.Linq.Expressions.Expression.Lambda<System.Func<object, object, object>>(call, opticParam, proofParam);
            return lambda.Compile();
        });
        return func(optic, proofInstance!);
    }

    //FindEvalMethod recursively searches the class and all interfaces for the generic Eval method
    //aligns with Java virtual dispatch semantics after type erasure
    private static MethodInfo? FindEvalMethod(Type type)
    {
        var method = type.GetMethod("Eval");
        if (method != null && method.IsGenericMethod) return method;
        foreach (var iface in type.GetInterfaces())
        {
            method = iface.GetMethod("Eval");
            if (method != null && method.IsGenericMethod) return method;
        }
        return type.BaseType != null ? FindEvalMethod(type.BaseType) : null;
    }
}
