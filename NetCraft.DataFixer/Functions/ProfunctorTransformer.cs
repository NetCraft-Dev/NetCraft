namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using T = NetCraft.DataFixer.Types;

//ProfunctorTransformer profunctor变换对应原版ProfunctorTransformer
//把TypedOptic转为PointFree<Func<Func<A,B>,Func<S,T>>>
public sealed class ProfunctorTransformer<S, T2, A, B> : PointFree<Func<Func<A, B>, Func<S, T2>>>
{
    private readonly TypedOptic<S, T2, A, B> _optic;

    public ProfunctorTransformer(TypedOptic<S, T2, A, B> optic)
    {
        _optic = optic;
    }

    public TypedOptic<S, T2, A, B> Optic => _optic;

    //castOuterUnchecked改外层类型不检查委托到optic.CastOuterUnchecked
    public ProfunctorTransformer<S2, T3, A, B> CastOuterUnchecked<S2, T3>(T.Type<S2> sType, T.Type<T3> tType)
        => new ProfunctorTransformer<S2, T3, A, B>(_optic.CastOuterUnchecked(sType, tType));

    //CastOuterUncheckedObject非泛型版用Unsafe.As绕过编译期类型检查
    //供PointFreeRule.SortProj/SortInj等反射调用对齐Java类型擦除语义
    public ProfunctorTransformer<object, object, A, B> CastOuterUncheckedObject(object sType, object tType)
    {
        var sObj = sType;
        var tObj = tType;
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sObj);
        var tCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref tObj);
        return new ProfunctorTransformer<object, object, A, B>(_optic.CastOuterUnchecked(sCast, tCast));
    }

    //type返回(A->B)->(S->T)的函数类型对应DSL.func(DSL.func(aType, bType), DSL.func(sType, tType))
    public override T.Type<Func<Func<A, B>, Func<S, T2>>> Type()
        => DSL.Func(DSL.Func(_optic.AType(), _optic.BType()), DSL.Func(_optic.SType(), _optic.TType()));

    public override string ToString(int level) => "Optic[" + _optic + "]";

    //eval用FunctionType作profunctor证明upCast后求值把A->B提升为S->T
    //原版靠Java类型擦除强转Optic<? super FunctionTypeInstance.Mu,...>后调eval
    //C#严格泛型不变量下强转失败用反射委托调用对齐Java虚方法分派
    //result运行时是Func<App2<Mu,object,object>,App2<Mu,object,object>>对齐Java类型擦除
    //用Unsafe.As绕过运行时类型检查强转为带A/B/S/T2的具体Func类型
    public override Func<DynamicOps<object>, Func<Func<A, B>, Func<S, T2>>> Eval()
    {
        var opticInstance = _optic.UpCast(typeof(FunctionTypeInstance.Mu)).Get();
        return _ => input =>
        {
            var boxed = FunctionType<A, B>.Create(input);
            var result = NetCraft.DataFixer.Optics.EvalCacheHelper.InvokeEval<FunctionTypes.Mu>(opticInstance!, FunctionTypeInstance.InstanceOf);
            var func = System.Runtime.CompilerServices.Unsafe.As<object, System.Func<App2<FunctionTypes.Mu, A, B>, App2<FunctionTypes.Mu, S, T2>>>(ref result);
            var appResult = func.Invoke(boxed);
            return FunctionType<S, T2>.GetFunc(appResult!);
        };
    }

    public override bool Equals(object? obj)
        => obj is ProfunctorTransformer<S, T2, A, B> other && Equals(_optic, other._optic);

    public override int GetHashCode() => _optic?.GetHashCode() ?? 0;
}
