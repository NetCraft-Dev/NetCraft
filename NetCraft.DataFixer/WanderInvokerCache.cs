namespace NetCraft.DataFixer;

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using NetCraft.DataFixer.Kinds;

//WanderInvokerCache caches the reflective delegate invocations for Traversal.Wander
//the actual traversal type may be Traversal<Pair<string,object>,...>, hard-cast via Unsafe.As to Traversal<object,object,...>
//under C#'s strict generic invariance the method table entries of the two closed types are not shared, so calling Wander directly throws EntryPointNotFoundException
//using an expression tree delegate cache bypasses the method table entry check, aligning with Java's erased virtual dispatch semantics
internal static class WanderInvokerCache
{
    //wanderInvoker caches (traversalType, FType, TMu2Type, inputFuncType) -> Func<object, object, object, object>
    //calls traversal.Wander<F,TMu2>(applicative, input) and returns wanderFunc
    private static readonly ConcurrentDictionary<(Type, Type, Type, Type), Func<object, object, object, object>> _wanderCache = new();

    //funcInvoker caches (wanderFuncType, argType) -> Func<object, object, object>
    //calls wanderFunc.Invoke(value) and returns the boxed result
    private static readonly ConcurrentDictionary<(Type, Type), Func<object, object, object>> _funcInvokeCache = new();

    public static object InvokeWander<FT, FR, F, TMu2>(
        object traversal, object applicative, Func<FT, App<F, FR>> input, object value)
        where F : K1 where TMu2 : IApplicativeMu
    {
        var wanderFunc = GetWanderFunc<FT, FR, F, TMu2>(traversal, applicative, input);
        return InvokeWanderFunc(wanderFunc, value);
    }

    //GetWanderFunc only calls traversal.Wander to return wanderFunc without invoking it
    //DimapTraversal.Wander needs to invoke wanderFunc lazily, aligning with vanilla semantics
    public static object GetWanderFunc<FT, FR, F, TMu2>(
        object traversal, object applicative, Func<FT, App<F, FR>> input)
        where F : K1 where TMu2 : IApplicativeMu
    {
        var traversalType = traversal.GetType();
        var inputFuncType = typeof(Func<FT, App<F, FR>>);
        var cacheKey = (traversalType, typeof(F), typeof(TMu2), inputFuncType);

        var wanderInvoker = _wanderCache.GetOrAdd(cacheKey, key =>
        {
            //finds the Wander method, which may live on an interface, aligning with Java's erased virtual dispatch
            var wanderMethod = FindWanderMethod(key.Item1)
                ?? throw new InvalidOperationException($"Wander method not found on {key.Item1.FullName}");
            var method = wanderMethod.MakeGenericMethod(key.Item2, key.Item3);

            var traversalParam = Expression.Parameter(typeof(object), "t");
            var applicativeParam = Expression.Parameter(typeof(object), "a");
            var inputParam = Expression.Parameter(typeof(object), "i");

            //the input parameter type method.GetParameters()[1].ParameterType is Func<A,App<F,B>>
            //the caller passes input as Func<FT,App<F,FR>>, with FT=A and FR=B matching
            //but across closed generic instantiations Func<FT,App<F,FR>> may differ from the method's expected type
            //so Unsafe.As bypasses the castclass runtime check, aligning with Java type erasure semantics
            var inputParamType = method.GetParameters()[1].ParameterType;
            var castInputMethod = typeof(WanderInvokerCache)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(inputParamType);

            //the applicative parameter type method.GetParameters()[0].ParameterType is Applicative<F,TMu2>
            //the caller passes an applicative implementing the Applicative<F,TMu2> interface
            //across closed generic instantiations Applicative<concrete F,concrete TMu2> may differ from the method's expected type
            //so Unsafe.As bypasses the castclass runtime check
            var applicativeParamType = method.GetParameters()[0].ParameterType;
            var castApplicativeMethod = typeof(WanderInvokerCache)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(applicativeParamType);

            var call = Expression.Call(
                Expression.Convert(traversalParam, key.Item1),
                method,
                Expression.Call(castApplicativeMethod, applicativeParam),
                Expression.Call(castInputMethod, inputParam));

            return Expression.Lambda<Func<object, object, object, object>>(
                Expression.Convert(call, typeof(object)),
                traversalParam, applicativeParam, inputParam).Compile();
        });

        return wanderInvoker(traversal, applicative, input!);
    }

    //InvokeWanderFunc calls wanderFunc(value) and returns the boxed result
    //DimapTraversal.Wander uses _g(c) as value to invoke after obtaining wanderFunc
    public static object InvokeWanderFunc(object wanderFunc, object value)
    {
        var wanderFuncType = wanderFunc.GetType();
        var funcInvokeKey = (wanderFuncType, value?.GetType() ?? typeof(object));

        var funcInvoker = _funcInvokeCache.GetOrAdd(funcInvokeKey, key =>
        {
            //wanderFunc is Func<S,App<F,T>>, where S's actual type is Pair<string,object> and so on
            //value is A=object at compile time and Pair<string,object> at runtime, matching S
            //so Unsafe.As bypasses the castclass runtime check, aligning with Java type erasure semantics
            var invokeMethod = key.Item1.GetMethod("Invoke")!;
            var paramType = invokeMethod.GetParameters()[0].ParameterType;

            var funcParam = Expression.Parameter(typeof(object), "f");
            var argParam = Expression.Parameter(typeof(object), "a");

            var castArgMethod = typeof(WanderInvokerCache)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(paramType);

            var call = Expression.Call(
                Expression.Convert(funcParam, key.Item1),
                invokeMethod,
                Expression.Call(castArgMethod, argParam));

            return Expression.Lambda<Func<object, object, object>>(
                Expression.Convert(call, typeof(object)),
                funcParam, argParam).Compile();
        });

        return funcInvoker(wanderFunc, value!);
    }

    //FindWanderMethod recursively searches the class and all interfaces for the generic Wander method
    private static MethodInfo? FindWanderMethod(Type type)
    {
        var method = type.GetMethod("Wander");
        if (method != null && method.IsGenericMethod) return method;
        foreach (var iface in type.GetInterfaces())
        {
            method = iface.GetMethod("Wander");
            if (method != null && method.IsGenericMethod) return method;
        }
        return type.BaseType != null ? FindWanderMethod(type.BaseType) : null;
    }

    //GetWanderFuncForWander calls the Wander method of a Wander interface instance and returns Func<S,App<F,T>>
    //WanderTraversal._wander is Wander<concrete S,T,A,B> at runtime, Unsafe.As cast to Wander<S,T,A,B>
    //under C#'s strict generic invariance the method table entries of the two closed types are not shared, so calling Wander directly throws EntryPointNotFoundException
    //using an expression tree delegate cache bypasses the method table entry check, aligning with Java's erased virtual dispatch semantics
    public static Func<S, App<F, T>> GetWanderFuncForWander<S, T, F, TMu2, A, B>(
        object wander, object applicative, Func<A, App<F, B>> input)
        where F : K1 where TMu2 : IApplicativeMu
    {
        var wanderType = wander.GetType();
        var cacheKey = (wanderType, typeof(F), typeof(TMu2), typeof(Func<A, App<F, B>>));

        var wanderInvoker = _wanderCache.GetOrAdd(cacheKey, key =>
        {
            var wanderMethod = FindWanderMethod(key.Item1)
                ?? throw new InvalidOperationException($"Wander method not found on {key.Item1.FullName}");
            var method = wanderMethod.MakeGenericMethod(key.Item2, key.Item3);

            var wanderParam = Expression.Parameter(typeof(object), "t");
            var applicativeParam = Expression.Parameter(typeof(object), "a");
            var inputParam = Expression.Parameter(typeof(object), "i");

            var inputParamType = method.GetParameters()[1].ParameterType;
            var castInputMethod = typeof(WanderInvokerCache)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(inputParamType);

            var applicativeParamType = method.GetParameters()[0].ParameterType;
            var castApplicativeMethod = typeof(WanderInvokerCache)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(applicativeParamType);

            var call = Expression.Call(
                Expression.Convert(wanderParam, key.Item1),
                method,
                Expression.Call(castApplicativeMethod, applicativeParam),
                Expression.Call(castInputMethod, inputParam));

            return Expression.Lambda<Func<object, object, object, object>>(
                Expression.Convert(call, typeof(object)),
                wanderParam, applicativeParam, inputParam).Compile();
        });

        var result = wanderInvoker(wander, applicative, (object)input!);
        return System.Runtime.CompilerServices.Unsafe.As<object, Func<S, App<F, T>>>(ref result!);
    }

    //CastTo uses Unsafe.As to bypass C#'s strict generic invariance, converting object to any type T
    //aligning with Java type erasure semantics and avoiding castclass runtime check failures
    private static T CastTo<T>(object obj)
    {
        var local = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
    }
}
