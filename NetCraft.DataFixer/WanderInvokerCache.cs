namespace NetCraft.DataFixer;

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using NetCraft.DataFixer.Kinds;

//WanderInvokerCache缓存Traversal.Wander的反射委托调用
//traversal实际类型可能是Traversal<Pair<string,object>,...>被Unsafe.As强转为Traversal<object,object,...>
//C#严格泛型不变量下两封闭类型方法表入口不共享直接调Wander抛EntryPointNotFoundException
//用表达式树委托缓存绕过方法表入口检查对齐Java类型擦除后虚方法分派语义
internal static class WanderInvokerCache
{
    //wanderInvoker缓存(traversalType, FType, TMu2Type, inputFuncType) -> Func<object, object, object, object>
    //调用traversal.Wander<F,TMu2>(applicative, input)返回wanderFunc
    private static readonly ConcurrentDictionary<(Type, Type, Type, Type), Func<object, object, object, object>> _wanderCache = new();

    //funcInvoker缓存(wanderFuncType, argType) -> Func<object, object, object>
    //调用wanderFunc.Invoke(value)返回boxed结果
    private static readonly ConcurrentDictionary<(Type, Type), Func<object, object, object>> _funcInvokeCache = new();

    public static object InvokeWander<FT, FR, F, TMu2>(
        object traversal, object applicative, Func<FT, App<F, FR>> input, object value)
        where F : K1 where TMu2 : IApplicativeMu
    {
        var wanderFunc = GetWanderFunc<FT, FR, F, TMu2>(traversal, applicative, input);
        return InvokeWanderFunc(wanderFunc, value);
    }

    //GetWanderFunc只调用traversal.Wander返回wanderFunc不调用
    //DimapTraversal.Wander内部需要wanderFunc延迟调用对齐原版语义
    public static object GetWanderFunc<FT, FR, F, TMu2>(
        object traversal, object applicative, Func<FT, App<F, FR>> input)
        where F : K1 where TMu2 : IApplicativeMu
    {
        var traversalType = traversal.GetType();
        var inputFuncType = typeof(Func<FT, App<F, FR>>);
        var cacheKey = (traversalType, typeof(F), typeof(TMu2), inputFuncType);

        var wanderInvoker = _wanderCache.GetOrAdd(cacheKey, key =>
        {
            //查找Wander方法可能在接口上对齐Java类型擦除后虚方法分派
            var wanderMethod = FindWanderMethod(key.Item1)
                ?? throw new InvalidOperationException($"Wander method not found on {key.Item1.FullName}");
            var method = wanderMethod.MakeGenericMethod(key.Item2, key.Item3);

            var traversalParam = Expression.Parameter(typeof(object), "t");
            var applicativeParam = Expression.Parameter(typeof(object), "a");
            var inputParam = Expression.Parameter(typeof(object), "i");

            //input参数类型method.GetParameters()[1].ParameterType是Func<A,App<F,B>>
            //调用方传input是Func<FT,App<F,FR>> FT=A FR=B匹配
            //但跨封闭泛型实例化下Func<FT,App<F,FR>>与方法期望类型可能不同
            //用Unsafe.As绕过castclass运行时检查对齐Java类型擦除语义
            var inputParamType = method.GetParameters()[1].ParameterType;
            var castInputMethod = typeof(WanderInvokerCache)
                .GetMethod(nameof(CastTo), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(inputParamType);

            //applicative参数类型method.GetParameters()[0].ParameterType是Applicative<F,TMu2>
            //调用方传applicative实现Applicative<F,TMu2>接口
            //跨封闭泛型实例化下Applicative<具体F,具体TMu2>与方法期望类型可能不同
            //用Unsafe.As绕过castclass运行时检查
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

    //InvokeWanderFunc调用wanderFunc(value)返回boxed结果
    //DimapTraversal.Wander拿到wanderFunc后用_g(c)作为value调用
    public static object InvokeWanderFunc(object wanderFunc, object value)
    {
        var wanderFuncType = wanderFunc.GetType();
        var funcInvokeKey = (wanderFuncType, value?.GetType() ?? typeof(object));

        var funcInvoker = _funcInvokeCache.GetOrAdd(funcInvokeKey, key =>
        {
            //wanderFunc是Func<S,App<F,T>> S实际类型Pair<string,object>等
            //value编译期是A=object运行时是Pair<string,object>匹配S
            //用Unsafe.As绕过castclass运行时检查对齐Java类型擦除语义
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

    //FindWanderMethod递归查找类与所有接口的Wander泛型方法
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

    //GetWanderFuncForWander调用Wander接口实例的Wander方法返回Func<S,App<F,T>>
    //WanderTraversal._wander运行时是Wander<具体S,T,A,B>被Unsafe.As cast为Wander<S,T,A,B>
    //C#严格泛型不变量下两封闭类型方法表入口不共享直接调Wander抛EntryPointNotFoundException
    //用表达式树委托缓存绕过方法表入口检查对齐Java类型擦除后虚方法分派语义
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

    //CastTo用Unsafe.As绕过C#严格泛型不变量让object转任意类型T
    //对齐Java类型擦除语义避免castclass运行时检查失败
    private static T CastTo<T>(object obj)
    {
        var local = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
    }
}
