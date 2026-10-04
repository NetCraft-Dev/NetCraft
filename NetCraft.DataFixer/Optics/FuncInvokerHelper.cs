namespace NetCraft.DataFixer.Optics;

using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

//FuncInvokerHelper缓存委托反射Invoke对齐Java类型擦除后虚方法分派
//委托invoke严格类型检查下Func<A,B>的A与运行时实际类型参数不匹配抛InvalidCastException
//用表达式树编译Func<object,object>包装委托invoke绕过运行时类型检查
internal static class FuncInvokerHelper
{
    //缓存(funcType, argType) -> Func<object, object, object>
    //第一参数是委托实例第二参数是参数值返回结果对象
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

            //arg运行时类型IdentityTraversal<A,B>与paramType App2<Mu<A,B>,object,object>不同
            //用Unsafe.As cast arg到paramType绕过委托invoke的CLR协变检查对齐Java类型擦除
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

    //CastTo用Unsafe.As绕过C#严格泛型不变量让object转任意类型T
    //对齐Java类型擦除语义避免castclass运行时检查失败
    private static T CastTo<T>(object obj)
    {
        var local = obj;
        return System.Runtime.CompilerServices.Unsafe.As<object, T>(ref local);
    }
}
