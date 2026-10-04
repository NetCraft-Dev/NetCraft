namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//Typed带类型值对应原版com.mojang.datafixers.Typed
//把Type<A>与ops与value绑定为可操作的Typed实例
public sealed class Typed<A>
{
    //type绑定的类型
    public T.Type<A> TypeValue { get; }
    //ops绑定的动态操作器
    public DynamicOps<object> Ops { get; }
    //value实际值
    public A Value { get; }

    public Typed(T.Type<A> type, DynamicOps<object> ops, A value)
    {
        TypeValue = type;
        Ops = ops;
        Value = value;
    }

    public override string ToString() => "Typed[" + Value + "]";

    //get按optic查找取得焦点值
    public FT Get<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.Forget<FT, FT, FT>(a => a);
        var boxed = field.Apply<Forgets.Mu<FT>>(ForgetInstance<FT>.InstanceOf, (App2<Forgets.Mu<FT>, FT, FT>)(object)forgetOptic);
        return Forgets.Unbox<FT, A, object>(boxed).Run(Value);
    }

    //getTyped按optic查找取得Typed
    public Typed<FT> GetTyped<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.Forget<FT, FT, FT>(a => a);
        var boxed = field.Apply<Forgets.Mu<FT>>(ForgetInstance<FT>.InstanceOf, (App2<Forgets.Mu<FT>, FT, FT>)(object)forgetOptic);
        var value = Forgets.Unbox<FT, A, object>(boxed).Run(Value);
        return new Typed<FT>(field.AType(), Ops, value);
    }

    //getOptional按optic查找取得可选值
    public Optional<FT> GetOptional<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.ForgetOpt<FT, FT, FT>(a => Optional<FT>.Of(a));
        var boxed = field.Apply<ForgetOpts.Mu<FT>>(ForgetOptInstance<FT>.InstanceOf, (App2<ForgetOpts.Mu<FT>, FT, FT>)(object)forgetOptic);
        return ForgetOpts.Unbox<FT, A, object>(boxed).Run(Value);
    }

    //getOrCreate按optic查找无值时用point默认
    public FT GetOrCreate<FT>(OpticFinder<FT> optic)
    {
        var optional = GetOptional(optic);
        var combined = DataFixUtils.Or(optional, () => optic.Type().Point(Ops));
        if (combined.IsPresent) return combined.Get();
        throw new InvalidOperationException("Could not create default value for type: " + optic.Type());
    }

    //getOrDefault按optic查找无值时用默认
    public FT GetOrDefault<FT>(OpticFinder<FT> optic, FT def)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.ForgetOpt<FT, FT, FT>(a => Optional<FT>.Of(a));
        var boxed = field.Apply<ForgetOpts.Mu<FT>>(ForgetOptInstance<FT>.InstanceOf, (App2<ForgetOpts.Mu<FT>, FT, FT>)(object)forgetOptic);
        return ForgetOpts.Unbox<FT, A, object>(boxed).Run(Value).OrElse(def);
    }

    //getOptionalTyped按optic查找取得可选Typed
    public Optional<Typed<FT>> GetOptionalTyped<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FT>)(object)findResult.GetLeft().Get();
        var forgetOptic = OpticsClass.ForgetOpt<FT, FT, FT>(a => Optional<FT>.Of(a));
        var boxed = field.Apply<ForgetOpts.Mu<FT>>(ForgetOptInstance<FT>.InstanceOf, (App2<ForgetOpts.Mu<FT>, FT, FT>)(object)forgetOptic);
        return ForgetOpts.Unbox<FT, A, object>(boxed).Run(Value).Map(v => new Typed<FT>(field.AType(), Ops, v));
    }

    //getOrCreateTyped按optic查找无值时用pointTyped默认
    public Typed<FT> GetOrCreateTyped<FT>(OpticFinder<FT> optic)
    {
        var optional = GetOptionalTyped(optic);
        var combined = DataFixUtils.Or(optional, () => optic.Type().PointTyped(Ops));
        if (combined.IsPresent) return combined.Get();
        throw new InvalidOperationException("Could not create default value for type: " + optic.Type());
    }

    //set按optic与newValue替换焦点
    public Typed<object> Set<FT>(OpticFinder<FT> optic, FT newValue)
        => Set<FT, FT>(optic, optic.Type(), newValue);

    //set按optic与新Type新值替换焦点
    public Typed<object> Set<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, FR newValue)
        => Set<FT, FR>(optic, new Typed<FR>(newType, Ops, newValue));

    //set按optic与新Typed替换焦点
    public Typed<object> Set<FT, FR>(OpticFinder<FT> optic, Typed<FR> newValue)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, newValue.TypeValue, false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<A, object, FT, FR>)(object)findResult.GetLeft().Get();
        return SetCap<object, FT, FR>(field, newValue);
    }

    //setCap用ReForgetC反向遗忘写入新值返回新Typed
    private Typed<B> SetCap<B, FT, FR>(TypedOptic<A, B, FT, FR> field, Typed<FR> newValue)
    {
        var reForgetOptic = OpticsClass.ReForgetC<FR, FT, FR>("set",
            Either<Func<FR, FR>, Func<FT, FR, FR>>.Left((FR fr) => fr));
        var boxed = field.Apply<ReForgetCs.Mu<FR>>(ReForgetCInstance<FR>.InstanceOf, (App2<ReForgetCs.Mu<FR>, FT, FR>)(object)reForgetOptic);
        var impl = ReForgetCs.Unbox<FR, FT, FR>((App2<ReForgetCs.Mu<FR>, FT, FR>)(object)boxed).Impl();
        B b;
        if (impl.IsLeft)
        {
            var f = impl.GetLeft().Get();
            b = (B)(object)f(newValue.Value!)!;
        }
        else
        {
            var f = impl.GetRight().Get();
            b = (B)(object)f((FT)(object)Value!, newValue.Value!)!;
        }
        return new Typed<B>(field.TType(), Ops, b);
    }

    //updateTyped按optic与Typed更新函数更新焦点
    public Typed<object> UpdateTyped<FT>(OpticFinder<FT> optic, Func<Typed<object>, Typed<object>> updater)
        => UpdateTypedImpl<FT, FT>(optic, optic.Type(), updater, false);

    //updateTyped按optic与新Type与Typed更新函数更新焦点对应原版updateTyped(optic,newType,fn)
    public Typed<object> UpdateTyped<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, Func<Typed<object>, Typed<object>> updater)
        => UpdateTypedImpl<FT, FR>(optic, newType, updater, false);

    //updateTypedImpl按optic与新Type与Typed更新函数更新焦点
    private Typed<object> UpdateTypedImpl<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, Func<Typed<object>, Typed<object>> updater, bool recurse)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, newType, recurse);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        //findResult返回TypedOptic<object,object,object,object>被FieldFinder cast成Type<object>导致类型擦除
        //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
        var fieldObj = (object)findResult.GetLeft().Get();
        var field = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<A, object, FT, FR>>(ref fieldObj);
        return UpdateCap<object, FT, FR>(field, ft =>
        {
            //optic.Type()返回Type<FT>实际可能是EmptyPartPassthrough等Type<Dynamic<object>>子类
            //C#严格泛型不变量下Type<Dynamic<object>>不继承Type<object>直接cast抛InvalidCastException
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var opticTypeObj = (object)optic.Type()!;
            var opticType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref opticTypeObj);
            var newValue = updater(new Typed<object>(opticType, Ops, (object)ft!));
            //newValue.TypeValue实际可能是EmptyPartPassthrough等Type<Dynamic<object>>子类
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var newTypeObj = (object)newValue.TypeValue!;
            var newTypeCasted = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<FR>>(ref newTypeObj);
            var bType = field.BType()!;
            var ifSameResult = bType.IfSame<FR>(newTypeCasted!, (FR)(object)newValue.Value!);
            return ifSameResult.IsPresent ? ifSameResult.Get() : throw new ArgumentException("Function didn't update to the expected type bType=" + bType + " newValueType=" + newTypeCasted);
        });
    }

    //update按optic与函数更新焦点
    public Typed<object> Update<FT>(OpticFinder<FT> optic, Func<FT, FT> updater)
        => UpdateImpl<FT, FT>(optic, optic.Type(), updater, false);

    //updateImpl按optic与新Type与函数更新焦点
    private Typed<object> UpdateImpl<FT, FR>(OpticFinder<FT> optic, T.Type<FR> newType, Func<FT, FR> updater, bool recurse)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, newType, recurse);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        //findResult返回TypedOptic<object,object,object,object>被FieldFinder cast成Type<object>导致类型擦除
        //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
        var fieldObj = (object)findResult.GetLeft().Get();
        var field = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<A, object, FT, FR>>(ref fieldObj);
        return UpdateCap<object, FT, FR>(field, updater);
    }

    //updateRecursiveTyped按递归optic与Typed更新函数更新焦点
    public Typed<object> UpdateRecursiveTyped<FT>(OpticFinder<FT> optic, Func<Typed<object>, Typed<object>> updater)
        => UpdateTypedImpl<FT, FT>(optic, optic.Type(), updater, true);

    //updateRecursive按递归optic与函数更新焦点
    public Typed<object> UpdateRecursive<FT>(OpticFinder<FT> optic, Func<FT, FT> updater)
        => UpdateImpl<FT, FT>(optic, optic.Type(), updater, true);

    //updateCap用Traversal.wander配合IdF应用updater返回新Typed
    private Typed<B> UpdateCap<B, FT, FR>(TypedOptic<A, B, FT, FR> field, Func<FT, FR> updater)
    {
        //UpCast返回object实际是Proj2等具体Optic运行时不是Optic<ITraversalPMu,...>
        //(object)cast再(Optic<ITraversalPMu,...>)cast运行时castclass抛InvalidCastException
        //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
        var opticObj = (object)field.UpCast(TypeClassesMarker.TraversalPToken)!.Get()!;
        var traversalOptic = System.Runtime.CompilerServices.Unsafe.As<object, Optic<ITraversalPMu, A, B, FT, FR>>(ref opticObj);
        var traversal = OpticsClass.ToTraversal<A, B, FT, FR>(traversalOptic)!;
        //traversal实际类型可能是Traversal<Pair<string,object>,Pair<string,object>,FT,FR>被Unsafe.As强转为Traversal<A,B,FT,FR>
        //C#严格泛型不变量下两封闭类型方法表入口不共享直接调Wander抛EntryPointNotFoundException
        //用反射委托缓存调用Wander对齐Java类型擦除后虚方法分派语义
        var boxed = WanderInvokerCache.InvokeWander<FT, FR, IdFs.Mu, IdFInstance.Mu>(traversal, IdFInstance.InstanceOf, ft => IdFs.Create<FR>(updater(ft)), Value!);
        //boxed是object实际是IdF实例Unsafe.As转App<IdFs.Mu,B>对齐Java类型擦除
        var boxedApp = System.Runtime.CompilerServices.Unsafe.As<object, App<IdFs.Mu, B>>(ref boxed);
        var b = IdFs.Get<B>(boxedApp);
        return new Typed<B>(field.TType(), Ops, b);
    }

    //getAllTyped按optic取得所有Typed
    public List<Typed<FT>> GetAllTyped<FT>(OpticFinder<FT> optic)
    {
        var findResult = optic.FindType((T.Type<object>)(object)TypeValue!, optic.Type(), false);
        if (findResult.IsRight) throw new InvalidOperationException("Field not found: " + findResult.GetRight().Get());
        var field = (TypedOptic<object, object, FT, object>)(object)findResult.GetLeft().Get();
        var all = GetAll<FT>(field);
        var result = new List<Typed<FT>>(all.Count);
        foreach (var ft in all)
        {
            result.Add(new Typed<FT>(optic.Type(), Ops, ft));
        }
        return result;
    }

    //getAll按TypedOptic取得所有焦点值
    //用Const<List<FT>>作Applicative配合ListMonoid累积所有焦点
    public List<FT> GetAll<FT>(TypedOptic<object, object, FT, object> field)
    {
        //UpCast返回object用Unsafe.As转Optic对齐Java类型擦除语义
        var opticObj = (object)field.UpCast(TypeClassesMarker.TraversalPToken)!.Get()!;
        var traversalOptic = System.Runtime.CompilerServices.Unsafe.As<object, Optic<ITraversalPMu, object, object, FT, object>>(ref opticObj);
        var traversal = OpticsClass.ToTraversal<object, object, FT, object>(traversalOptic)!;
        var constInstance = new ConstInstance<List<FT>>(Monoids.ListMonoid<FT>());
        //traversal实际类型可能被Unsafe.As强转直接调Wander抛EntryPointNotFoundException
        //用WanderInvokerCache反射委托缓存对齐Java类型擦除后虚方法分派语义
        var boxed = WanderInvokerCache.InvokeWander<FT, object, Consts.Mu<List<FT>>, ConstInstance<List<FT>>.Mu>(
            traversal, constInstance, ft => Consts.Create<List<FT>, object>(new List<FT> { ft }), (object)Value!);
        //boxed是object实际是Const<List<FT>>实例Unsafe.As转App<Consts.Mu<List<FT>>,object>对齐Java类型擦除
        var boxedApp = System.Runtime.CompilerServices.Unsafe.As<object, App<Consts.Mu<List<FT>>, object>>(ref boxed);
        return Consts.Unbox<List<FT>, object>(boxedApp);
    }

    //out递归点类型展开到unfold
    public Typed<A> Out()
    {
        if (TypeValue is not RecursivePoint.RecursivePointType<A>)
        {
            throw new ArgumentException("Not recursive");
        }
        var unfold = ((RecursivePoint.RecursivePointType<A>)TypeValue).Unfold();
        return new Typed<A>(unfold, Ops, Value);
    }

    //inj1注入Either左
    public Typed<Either<A, B>> Inj1<B>(T.Type<B> type)
    {
        var inj1 = OpticsClass.Inj1<A, B, A>();
        var eitherValue = inj1.Build(Value);
        return new Typed<Either<A, B>>(DSL.Or(TypeValue, type), Ops, eitherValue);
    }

    //inj2注入Either右
    public Typed<Either<B, A>> Inj2<B>(T.Type<B> type)
    {
        var inj2 = OpticsClass.Inj2<B, A, A>();
        var eitherValue = inj2.Build(Value);
        return new Typed<Either<B, A>>(DSL.Or(type, TypeValue), Ops, eitherValue);
    }

    //pair组合两个Typed为Pair用Util.Pair对齐DSL.And返回类型
    public static Typed<NetCraft.DataFixer.Util.Pair<A, B>> Pair<A, B>(Typed<A> first, Typed<B> second)
        => new Typed<NetCraft.DataFixer.Util.Pair<A, B>>(DSL.And(first.TypeValue, second.TypeValue), first.Ops, NetCraft.DataFixer.Util.Pair<A, B>.Of(first.Value, second.Value));

    public T.Type<A> GetType() => TypeValue;
    public DynamicOps<object> GetOps() => Ops;
    public A GetValue() => Value;

    //write把value写回Dynamic
    public DataResult<Dynamic<object>> Write() => TypeValue.WriteDynamic(Ops, Value);
}
