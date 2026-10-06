namespace NetCraft.DataFixer.Functions;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Types.Templates;

//Fold recursive fold maps to vanilla com.mojang.datafixers.functions.Fold
//folds RecursivePointType<A> into RecursivePointType<B> using an algebra
public sealed class Fold<A, B> : PointFree<Func<A, B>>
{
    private readonly RecursivePoint.RecursivePointType<A> _aType;
    private readonly RecursivePoint.RecursivePointType<B> _bType;
    private readonly Algebra _algebra;
    private readonly int _index;

    public Fold(RecursivePoint.RecursivePointType<A> aType, RecursivePoint.RecursivePointType<B> bType, Algebra algebra, int index)
    {
        _aType = aType;
        _bType = bType;
        _algebra = algebra;
        _index = index;
    }

    public RecursivePoint.RecursivePointType<A> AType => _aType;
    public RecursivePoint.RecursivePointType<B> BType => _bType;
    public Algebra Algebra => _algebra;
    public int Index => _index;

    //type returns the aType->bType function type, maps to DSL.func(aType, bType)
    public override T.Type<Func<A, B>> Type()
        => DSL.Func(_aType, _bType);

    //all applies the rule to the function at each index of the algebra; rebuilds Fold if any changes
    public override Optional<PointFree<Func<A, B>>> All(PointFreeRule rule)
    {
        var familySize = _aType.Family().Size();
        var newAlgebra = new List<RewriteResult<object, object>>(familySize);
        var changed = false;
        for (int i = 0; i < familySize; i++)
        {
            var view = _algebra.Apply(i);
            var function = view.View().Function;
            var rewrite = rule.RewriteOrNop(function!);
            if (!ReferenceEquals(rewrite, function))
            {
                newAlgebra.Add(Cap(view, rewrite));
                changed = true;
            }
            else
            {
                newAlgebra.Add(view);
            }
        }
        if (changed)
        {
            return Optional<PointFree<Func<A, B>>>.Of(
                new Fold<A, B>(_aType, _bType, new ListAlgebra("Rewrite all", newAlgebra), _index));
        }
        return Optional<PointFree<Func<A, B>>>.Empty();
    }

    //cap preserves recData and wraps rewrite into a View to build a new RewriteResult
    private static RewriteResult<object, object> Cap(
        RewriteResult<object, object> view,
        PointFree<Func<object, object>> rewrite)
        => RewriteResult<object, object>.Create(new View<object, object>(rewrite, view.View().Type(), view.View().NewType()), view.RecData());

    //eval builds the rewrite function for the index via family.template.hmap and fold, composes it with the original algebra, then evaluates
    //HMAP_CACHE caches the template.hmap result so each Eval does not rebuild the Comp chain, which would invalidate the evalCached cache and recurse forever
    public override Func<DynamicOps<object>, Func<A, B>> Eval()
        => ops => a =>
        {
            var family = _aType.Family();
            var newFamily = _bType.Family();
            var key = new HmapCacheKey(family, newFamily, _algebra);
            Func<int, RewriteResult<object, object>> hmapped;
            lock (HMAP_CACHE)
            {
                if (!HMAP_CACHE.TryGetValue(key, out hmapped!))
                {
                    hmapped = family.Template().Hmap(family, family.Fold(_algebra!, newFamily));
                    HMAP_CACHE[key] = hmapped;
                }
            }
            var applyKey = new HmapApplyKey(hmapped, _index);
            RewriteResult<object, object> result;
            lock (HMAP_APPLY_CACHE)
            {
                if (!HMAP_APPLY_CACHE.TryGetValue(applyKey, out result!))
                {
                    result = hmapped(_index);
                    HMAP_APPLY_CACHE[applyKey] = result;
                }
            }
            var eval = CapResult(result);
            return eval.EvalCached()(ops)(a!);
        };

    //HmapCacheKey compares by family+newFamily+algebra structure to cache the template.hmap result
    private readonly record struct HmapCacheKey(RecursiveTypeFamily Family, RecursiveTypeFamily NewFamily, Algebra Algebra);
    //HmapApplyKey caches hmapped(index) by hmapped+index
    private readonly record struct HmapApplyKey(Func<int, RewriteResult<object, object>> Hmapped, int Index);
    private static readonly Dictionary<HmapCacheKey, Func<int, RewriteResult<object, object>>> HMAP_CACHE = new();
    private static readonly Dictionary<HmapApplyKey, RewriteResult<object, object>> HMAP_APPLY_CACHE = new();

    //capResult composes the intermediate RewriteResult with the result at the algebra index
    private PointFree<Func<A, B>> CapResult(RewriteResult<object, object> resResult)
    {
        var applied = _algebra!.Apply(_index);
        var op = (RewriteResult<A, B>)(object)applied!;
        var opFunc = op?.View()?.Function;
        var resFunc = resResult.View().Function;
        if (opFunc is null || resFunc is null)
        {
            //nop short-circuit: if either is null, return the other, aligning with vanilla fold semantics
            if (opFunc is null)
            {
                var resObj = (object)resFunc!;
                return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, B>>>(ref resObj);
            }
            var opObj = (object)opFunc!;
            return System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, B>>>(ref opObj);
        }
        //opFunc/resFunc may actually be a PointFree subclass such as Apply, so casting to PointFree<Func<...>> fails
        //use Unsafe.As to bypass the runtime type check and align with Java type erasure
        var opFuncObj = (object)opFunc;
        var opFuncCasted = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<object, B>>>(ref opFuncObj);
        var resFuncObj = (object)resFunc;
        var resFuncCasted = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A, object>>>(ref resFuncObj);
        return Functions.Comp(opFuncCasted, resFuncCasted);
    }

    public override string ToString(int level)
        => "fold(" + _aType + ", " + _index + ", \n" + Indent(level + 1) + _algebra.ToString(level + 1) + "\n" + Indent(level) + ")";

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not Fold<A, B> other) return false;
        return Equals(_aType, other._aType) && Equals(_bType, other._bType) && Equals(_algebra, other._algebra);
    }

    public override int GetHashCode()
    {
        var result = _aType?.GetHashCode() ?? 0;
        result = 31 * result + (_bType?.GetHashCode() ?? 0);
        result = 31 * result + (_algebra?.GetHashCode() ?? 0);
        return result;
    }
}
