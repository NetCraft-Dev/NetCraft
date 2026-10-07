namespace NetCraft.DataFixer.Optics;

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

//FuncInvokerHelper caches delegate reflective Invoke, aligning with Java virtual dispatch after type erasure
//under strict type checking, delegate invoke throws InvalidCastException when Func<A,B>'s A does not match the actual runtime type parameter
//compiles a Func<object,object> with an expression tree to wrap delegate invoke, bypassing the runtime type check
internal static class FuncInvokerHelper
{
    //caches (funcType, argType) -> Func<object, object, object>
    //the first argument is the delegate instance, the second is the argument value, and it returns the result object
    private static readonly ConcurrentDictionary<(Type funcType, Type argType), Func<object, object, object>> _invokeCache = new();

    public static object Invoke(object func, object arg)
    {
        var funcType = func.GetType();
        var argType = arg?.GetType() ?? typeof(object);
        var invoker = _invokeCache.GetOrAdd((funcType, argType), key =>
        {
            var invokeMethod = key.funcType.GetMethod("Invoke")!;
            var paramType = invokeMethod.GetParameters()[0].ParameterType;

            var funcParam = Expression.Parameter(typeof(object), "f");
            var argParam = Expression.Parameter(typeof(object), "a");

            //the runtime type of arg, IdentityTraversal<A,B>, differs from paramType App2<Mu<A,B>,object,object>
            //use Unsafe.As to cast arg to paramType, bypassing the delegate invoke CLR covariance check and aligning with Java type erasure
            var castArgMethod = typeof(FuncInvokerHelper)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(paramType);

            var call = Expression.Call(
                Expression.Convert(funcParam, key.funcType),
                invokeMethod,
                Expression.Call(castArgMethod, argParam));

            return Expression.Lambda<Func<object, object, object>>(
                Expression.Convert(call, typeof(object)),
                funcParam, argParam).Compile();
        });
        return invoker(func, arg!);
    }

    //CastTo uses Unsafe.As to bypass C# strict generic invariance, converting object to any type T
    //aligns with Java type erasure semantics, avoiding castclass runtime check failure
    private static T CastTo<T>(object obj)
    {
        var local = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
    }
}
