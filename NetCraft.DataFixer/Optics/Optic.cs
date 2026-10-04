namespace NetCraft.DataFixer.Optics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NetCraft.DataFixer.Kinds;

//光学接口核心组合子对应原版com.mojang.datafixers.optics.Optic
//Proof是证明类型约束可接受的最弱profunctor
//S/T是源/目标类型A/B是焦点/新值
public interface Optic<Proof, S, T, A, B> where Proof : K1
{
    //eval接收profunctor证明返回App2<P,A,B>->App2<P,S,T>的函数
    Func<App2<P, A, B>, App2<P, S, T>> Eval<P>(App<Proof, P> proof) where P : K2;
}

//组合光学对应原版Optic.CompositionOptic
//持有一组optic从右到左链式组合函数
public sealed record CompositionOptic<Proof, S, T, A, B>(IReadOnlyList<object> Optics) : Optic<Proof, S, T, A, B> where Proof : K1
{
    //eval从右到左收集每个optic的eval函数后链式应用
    //用表达式树编译委托缓存避免每次反射Invoke
    public Func<App2<P, A, B>, App2<P, S, T>> Eval<P>(App<Proof, P> proof) where P : K2
    {
        //用object[]存func避免List<Func<...>>内部数组协变检查抛ArrayTypeMismatchException
        //func运行时类型带具体泛型参数与Func<App2<P,object,object>,App2<P,object,object>>无继承关系
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

//EvalCacheHelper跨Optic共享表达式树缓存避免重复Invoke反射
//对应原版Java类型擦除后虚方法分派C#用编译委托模拟
internal static class EvalCacheHelper
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type opticType, Type pType), System.Func<object, object, object>> _evalCache = new();

    //ForceCast用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
    //proof运行时是FunctionTypeInstance实现App<FunctionTypeInstance.Mu,P>接口
    //但Eval期望App<ICartesianMu,P>等不同封闭泛型类型C#严格泛型不变量下强转失败
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
            //Eval可能是显式接口实现方法名带接口前缀GetMethod("Eval")找不到
            //遍历所有接口查找名为Eval的泛型方法对齐Java虚方法分派语义
            MethodInfo? method = FindEvalMethod(key.opticType);
            if (method == null)
            {
                throw new InvalidOperationException($"Eval method not found on {key.opticType.FullName}");
            }
            method = method.MakeGenericMethod(key.pType);
            var proofType = method.GetParameters()[0].ParameterType;
            //表达式树编译(o,p)=>optic.Eval<P>(ForceCast<App<Proof,P>>(p))
            //ForceCast用Unsafe.As绕过proof参数运行时类型检查对齐Java类型擦除语义
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

    //FindEvalMethod递归查找类与所有接口的Eval泛型方法
    //对齐Java类型擦除后虚方法分派语义
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
