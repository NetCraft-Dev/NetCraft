namespace NetCraft.DataFixer.Functions;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using T = NetCraft.DataFixer.Types;

//ProfunctorTransformer profunctor transform maps to vanilla ProfunctorTransformer
//converts a TypedOptic into PointFree<Func<Func<A,B>,Func<S,T>>>
public sealed class ProfunctorTransformer<S, T2, A, B> : PointFree<Func<Func<A, B>, Func<S, T2>>>
{
    private readonly TypedOptic<S, T2, A, B> _optic;

    public ProfunctorTransformer(TypedOptic<S, T2, A, B> optic)
    {
        _optic = optic;
    }

    public TypedOptic<S, T2, A, B> Optic => _optic;

    //castOuterUnchecked changes the outer type unchecked, delegating to optic.CastOuterUnchecked
    public ProfunctorTransformer<S2, T3, A, B> CastOuterUnchecked<S2, T3>(T.Type<S2> sType, T.Type<T3> tType)
        => new ProfunctorTransformer<S2, T3, A, B>(_optic.CastOuterUnchecked(sType, tType));

    //CastOuterUncheckedObject is a non-generic version using Unsafe.As to bypass compile-time type checking
    //used by reflective calls such as PointFreeRule.SortProj/SortInj, aligning with Java type erasure semantics
    public ProfunctorTransformer<object, object, A, B> CastOuterUncheckedObject(object sType, object tType)
    {
        var sObj = sType;
        var tObj = tType;
        var sCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sObj);
        var tCast = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref tObj);
        return new ProfunctorTransformer<object, object, A, B>(_optic.CastOuterUnchecked(sCast, tCast));
    }

    //type returns the (A->B)->(S->T) function type, maps to DSL.func(DSL.func(aType, bType), DSL.func(sType, tType))
    public override T.Type<Func<Func<A, B>, Func<S, T2>>> Type()
        => DSL.Func(DSL.Func(_optic.AType(), _optic.BType()), DSL.Func(_optic.SType(), _optic.TType()));

    public override string ToString(int level) => "Optic[" + _optic + "]";

    //eval uses FunctionType as the profunctor proof, evaluates after upCast, lifting A->B to S->T
    //vanilla relies on Java type erasure to cast to Optic<? super FunctionTypeInstance.Mu,...> then calls eval
    //under C# strict generic invariance the cast fails, so a reflective delegate call aligns with Java virtual dispatch
    //at runtime result is Func<App2<Mu,object,object>,App2<Mu,object,object>>, aligning with Java type erasure
    //use Unsafe.As to bypass the runtime type check and cast to the concrete Func type with A/B/S/T2
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
