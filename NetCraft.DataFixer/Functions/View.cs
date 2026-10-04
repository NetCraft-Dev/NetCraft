namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//View视图对应原版com.mojang.datafixers.View
    //表示A->B的转换函数与输入输出类型
public sealed class View<A, B>
{
    //function转换函数的PointFree表示类型为Func<A,B>
    public PointFree<Func<A, B>>? Function { get; }

    //oldType输入类型A的Type
    public T.Type<A> OldTypeValue { get; }

    //newType目标类型B的Type
    public T.Type<B> NewTypeValue { get; }

    public View(PointFree<Func<A, B>>? function, T.Type<A> oldType, T.Type<B> newType)
    {
        Function = function;
        OldTypeValue = oldType;
        NewTypeValue = newType;
    }

    //create工厂方法
    public static View<A, B> Create(PointFree<Func<A, B>>? function, T.Type<A> oldType, T.Type<B> newType)
        => new(function, oldType, newType);

    //create带name的工厂方法对应原版View.create(name, type, newType, function)
    //内部用Functions.fun包装为FunctionWrapper
    public static View<A, B> Create(string name, T.Type<A> oldType, T.Type<B> newType, Func<DynamicOps<object>, Func<A, B>> function)
        => new(Functions.Fun(name, function, oldType, newType), oldType, newType);

    //nopView用Id作为function保证oldType=newType=type对齐原版View.nopView
    //不能再用null因为Cap1需要NewType()作为下一条rule输入
    public static View<A, B> NopView(T.Type<A> type)
    {
        var idObj = (object)Functions.Id(type!);
        var idAB = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, B>>>(ref idObj);
        var typeBObj = (object)type!;
        var typeB = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<B>>(ref typeBObj);
        return new View<A, B>(idAB, type, typeB);
    }

    //isNop判断function是否为Id单位函数
    public bool IsNop() => Functions.IsIdUnchecked(Function);

    //type返回输入类型A
    public T.Type<A> Type() => OldTypeValue;

    //newType返回输出类型B
    public T.Type<B> NewType() => NewTypeValue;

    //rewrite对function应用PointFreeRule命中则重建View
    public Optional<View<A, B>> Rewrite(PointFreeRule rule)
    {
        if (Function is null) return Optional<View<A, B>>.Empty();
        var opt = rule.Rewrite(Function!);
        return opt.Map(f => new View<A, B>(f, OldTypeValue, NewTypeValue));
    }

    //compose把that的输出接到this的输入返回C->B的View
    //C#严格泛型下Id<A>不能直接强转PointFree<Func<C,B>>用Unsafe.As绕过对齐Java类型擦除
    public View<C, B> Compose<C>(View<C, A> that)
    {
        if (IsNop())
        {
            var thatFuncObj = (object)that.Function!;
            var thatFunc = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<C, B>>>(ref thatFuncObj);
            return new View<C, B>(thatFunc, that.OldTypeValue, NewTypeValue);
        }
        if (that.IsNop())
        {
            var funcObj = (object)Function!;
            var func = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<C, B>>>(ref funcObj);
            var oldTypeObj = (object)OldTypeValue;
            var oldType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<C>>(ref oldTypeObj);
            return new View<C, B>(func, oldType, NewTypeValue);
        }
        var thisFuncObj = (object)Function!;
        var thisFunc = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, B>>>(ref thisFuncObj);
        var thatFuncObj2 = (object)that.Function!;
        var thatFunc2 = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<C, A>>>(ref thatFuncObj2);
        var comp = Functions.Comp(thisFunc, thatFunc2);
        return new View<C, B>(comp, that.OldTypeValue, NewTypeValue);
    }
}
