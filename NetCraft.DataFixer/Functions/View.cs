namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;

//View maps to vanilla com.mojang.datafixers.View
    //represents the A->B conversion function together with the input and output types
public sealed class View<A, B>
{
    //function is the PointFree representation of the conversion function, typed Func<A,B>
    public PointFree<Func<A, B>>? Function { get; }

    //oldType is the Type of the input type A
    public T.Type<A> OldTypeValue { get; }

    //newType is the Type of the target type B
    public T.Type<B> NewTypeValue { get; }

    public View(PointFree<Func<A, B>>? function, T.Type<A> oldType, T.Type<B> newType)
    {
        Function = function;
        OldTypeValue = oldType;
        NewTypeValue = newType;
    }

    //create factory method
    public static View<A, B> Create(PointFree<Func<A, B>>? function, T.Type<A> oldType, T.Type<B> newType)
        => new(function, oldType, newType);

    //create with a name, maps to vanilla View.create(name, type, newType, function)
    //internally wraps it as a FunctionWrapper via Functions.fun
    public static View<A, B> Create(string name, T.Type<A> oldType, T.Type<B> newType, Func<DynamicOps<object>, Func<A, B>> function)
        => new(Functions.Fun(name, function, oldType, newType), oldType, newType);

    //nopView uses Id as function to guarantee oldType=newType=type, aligning with vanilla View.nopView
    //null can no longer be used because Cap1 needs NewType() as the input of the next rule
    public static View<A, B> NopView(T.Type<A> type)
    {
        var idObj = (object)Functions.Id(type!);
        var idAB = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, B>>>(ref idObj);
        var typeBObj = (object)type!;
        var typeB = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<B>>(ref typeBObj);
        return new View<A, B>(idAB, type, typeB);
    }

    //isNop checks whether function is the Id identity function
    public bool IsNop() => Functions.IsIdUnchecked(Function);

    //type returns the input type A
    public T.Type<A> Type() => OldTypeValue;

    //newType returns the output type B
    public T.Type<B> NewType() => NewTypeValue;

    //rewrite applies a PointFreeRule to function and rebuilds the View on a match
    public Optional<View<A, B>> Rewrite(PointFreeRule rule)
    {
        if (Function is null) return Optional<View<A, B>>.Empty();
        var opt = rule.Rewrite(Function!);
        return opt.Map(f => new View<A, B>(f, OldTypeValue, NewTypeValue));
    }

    //compose connects that's output to this's input and returns a C->B View
    //under C# strict generics Id<A> cannot be cast directly to PointFree<Func<C,B>>; use Unsafe.As to bypass it, aligning with Java type erasure
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
