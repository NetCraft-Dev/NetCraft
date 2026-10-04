namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;

//Functions函数工厂对应原版com.mojang.datafixers.functions.Functions
//提供comp/fun/app/fold/in/out/id等PointFree构造入口
public abstract class Functions
{
    //comp复合两个PointFree若一侧为Id直接返回另一侧否则合并Comp数组
    public static PointFree<Func<A, C>> Comp<A, B, C>(PointFree<Func<B, C>> f1, PointFree<Func<A, B>> f2)
    {
        if (IsIdUnchecked(f1))
        {
            return AsPF<A, C>(f2);
        }
        if (IsIdUnchecked(f2))
        {
            return AsPF<A, C>(f1);
        }
        //Unsafe.As绕过泛型不变量对齐Java类型擦除Type<?>语义
        object f1TypeObj = f1.Type();
        var f1FuncType = System.Runtime.CompilerServices.Unsafe.As<object, T.Func<B, C>>(ref f1TypeObj);
        object f2TypeObj = f2.Type();
        var f2FuncType = System.Runtime.CompilerServices.Unsafe.As<object, T.Func<A, B>>(ref f2TypeObj);
        var type = DSL.Func(f2FuncType.First(), f1FuncType.Second());

        if (f1 is Comp<B, C> comp1 && f2 is Comp<A, B> comp2)
        {
            var functions = new object[comp1.Functions().Length + comp2.Functions().Length];
            Array.Copy(comp1.Functions(), 0, functions, 0, comp1.Functions().Length);
            Array.Copy(comp2.Functions(), 0, functions, comp1.Functions().Length, comp2.Functions().Length);
            return new Comp<A, C>(functions, type);
        }
        if (f1 is Comp<B, C> comp1Only)
        {
            var functions = new object[comp1Only.Functions().Length + 1];
            Array.Copy(comp1Only.Functions(), 0, functions, 0, comp1Only.Functions().Length);
            functions[functions.Length - 1] = f2;
            return new Comp<A, C>(functions, type);
        }
        if (f2 is Comp<A, B> comp2Only)
        {
            var functions = new object[1 + comp2Only.Functions().Length];
            functions[0] = f1;
            Array.Copy(comp2Only.Functions(), 0, functions, 1, comp2Only.Functions().Length);
            return new Comp<A, C>(functions, type);
        }
        return new Comp<A, C>(
            new object[] { f1, f2 },
            type);
    }

    //AsPF用Unsafe.As把任意PointFree<B>当PointFree<Func<A,C>>用对齐Java类型擦除
    private static PointFree<Func<A, C>> AsPF<A, C>(object function)
    {
        var obj = function;
        return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, C>>>(ref obj);
    }

    //AsFuncObject用Unsafe.As把任意PointFree<B>当PointFree<Func<object,object>>用对齐Java类型擦除
    private static PointFree<Func<object, object>> AsFuncObject(object function)
    {
        var obj = function;
        return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, object>>>(ref obj);
    }

    //fun命名包装普通函数
    public static PointFree<Func<A, B>> Fun<A, B>(string name, Func<DynamicOps<object>, Func<A, B>> fun, T.Type<A> input, T.Type<B> output)
        => new FunctionWrapper<A, B>(name, fun, input, output);

    //app函数应用
    public static PointFree<B> App<A, B>(PointFree<Func<A, B>> fun, PointFree<A> arg)
        => new Apply<A, B>(fun, arg);

    //profunctorTransformer构造optic的profunctor变换
    public static PointFree<Func<Func<A, B>, Func<S, T>>> ProfunctorTransformer<S, T, A, B>(TypedOptic<S, T, A, B> lens)
        => new ProfunctorTransformer<S, T, A, B>(lens);

    //bang构造丢弃函数
    public static Bang<TA> Bang<TA>(T.Type<TA> type) => new(type);

    //in构造递归点的入向View
    public static PointFree<Func<TA, TA>> In<TA>(NetCraft.DataFixer.Types.Templates.RecursivePoint.RecursivePointType<TA> type)
        => new In<TA>(type);

    //out构造递归点的出向View
    public static PointFree<Func<TA, TA>> Out<TA>(NetCraft.DataFixer.Types.Templates.RecursivePoint.RecursivePointType<TA> type)
        => new Out<TA>(type);

    //fold构造递归折叠
    public static PointFree<Func<TA, TB>> Fold<TA, TB>(
        NetCraft.DataFixer.Types.Templates.RecursivePoint.RecursivePointType<TA> aType,
        NetCraft.DataFixer.Types.Templates.RecursivePoint.RecursivePointType<TB> bType,
        NetCraft.DataFixer.Types.Families.Algebra algebra,
        int index)
        => new Fold<TA, TB>(aType, bType, algebra, index);

    //id构造单位函数
    public static PointFree<Func<TA, TA>> Id<TA>(T.Type<TA> type)
        => new Id<TA>(DSL.Func(type, type));

    //isId判断是否为单位函数对齐原版function instanceof Id<?>
    public static bool IsId(PointFree<object>? function)
        => IsIdUnchecked(function);

    //isIdUnchecked反射检查任意Id<X>避免PointFree泛型强转异常
    internal static bool IsIdUnchecked(object? function)
    {
        if (function is null) return false;
        var type = function.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Id<>);
    }
}
