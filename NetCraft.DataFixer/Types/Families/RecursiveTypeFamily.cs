namespace NetCraft.DataFixer.Types.Families;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//RecursiveTypeFamily recursive type family maps to vanilla RecursiveTypeFamily
//built from a template, returns RecursivePointType per index, used for recursive types
public sealed class RecursiveTypeFamily : TypeFamily
{
    private readonly string _name;
    private readonly TypeTemplate _template;
    private readonly int _size;
    private readonly Dictionary<int, object> _types = new();
    private readonly int _hashCode;

    public RecursiveTypeFamily(string name, TypeTemplate template)
    {
        _name = name;
        _template = template;
        _size = template.Size();
        _hashCode = template?.GetHashCode() ?? 0;
    }

    public string Name() => _name;
    public TypeTemplate Template() => _template;
    public int Size() => _size;

    //buildMuType finds or constructs the family for the new type
    public object BuildMuType<A>(T.Type<A> newType, RecursiveTypeFamily? newFamily)
    {
        if (newFamily == null)
        {
            var newTypeTemplate = newType!.Template();
            if (Equals(_template, newTypeTemplate))
            {
                newFamily = this;
            }
            else
            {
                newFamily = new RecursiveTypeFamily("ruled " + _name, newTypeTemplate!);
            }
        }
        RecursivePoint.RecursivePointType<A>? newMuType = null;
        for (int i1 = 0; i1 < newFamily._size; i1++)
        {
            var type = newFamily.Apply(i1);
            var unfold = ((RecursivePoint.RecursivePointType<object>)type).Unfold();
            if (newType!.Equals(unfold, true, false))
            {
                newMuType = (RecursivePoint.RecursivePointType<A>)(object)type!;
                break;
            }
        }
        if (newMuType == null)
        {
            throw new InvalidOperationException("Couldn't determine the new type properly");
        }
        return newMuType;
    }

    //fold produces a function from index to RewriteResult using an algebra
    public Func<int, RewriteResult<object, object>> Fold(Algebra algebra, RecursiveTypeFamily newFamily)
        => index =>
        {
            var result = algebra.Apply(index);
            var func = FoldUnchecked<object, object>(this, newFamily, algebra, index);
            //aligns with vanilla View.create(fold), passing only function
            //type/newType are derived from func.type() as RecursivePointType rather than the CheckType in result.view
            var funcType = (NetCraft.DataFixer.Types.Func<object, object>)func.Type()!;
            return RewriteResult<object, object>.Create(
                View<object, object>.Create(
                    func,
                    funcType.First(),
                    funcType.Second()),
                result.RecData());
        };

    //foldUnchecked takes the family's two-sided types at the index to build the folded PointFree
    private static PointFree<Func<A, B>> FoldUnchecked<A, B>(
        RecursiveTypeFamily family, RecursiveTypeFamily newFamily, Algebra algebra, int index)
    {
        var type = (RecursivePoint.RecursivePointType<A>)(object)family.Apply(index)!;
        var newType = (RecursivePoint.RecursivePointType<B>)(object)newFamily.Apply(index)!;
        return Functions.Fold<A, B>(type, newType, algebra, index);
    }

    //apply returns the RecursivePointType at the given index, constructing and caching it
    public T.Type<object> Apply(int index)
    {
        if (index < 0) throw new IndexOutOfRangeException();
        if (_types.TryGetValue(index, out var cached))
            return (T.Type<object>)cached!;
        var type = new RecursivePoint.RecursivePointType<object>(this, index, () =>
        {
            var family = _template.Apply(this);
            return family.Apply(index);
        });
        _types[index] = type;
        return type;
    }

    //findType looks up the child type optic at the given index
    public Either<object, T.Type<object>.FieldNotFoundException> FindType<A, B>(
        int index, T.Type<A> aType, T.Type<B> bType, T.Type<object>.TypeMatcher<A, B> matcher, bool recurse)
    {
        var applyResult = Apply(index);
        var unfold = ((RecursivePoint.RecursivePointType<object>)applyResult).Unfold();
        return unfold.FindType(aType, bType, matcher, false)
            .MapLeft(o => (object)o)
            .FlatMap(optic =>
        {
            var typedOptic = (TypedOptic<object, object, A, B>)optic!;
            var nc = typedOptic.TType().Template();
            var newFamily = new RecursiveTypeFamily(_name, nc!);
            var sType = (RecursivePoint.RecursivePointType<object>)(object)applyResult!;
            var tType = (RecursivePoint.RecursivePointType<object>)(object)newFamily.Apply(index)!;

            if (recurse)
            {
                var fo = new List<FamilyOptic<A, B>>();
                //FamilyOptic is a class, not a delegate lambda, so it must be wrapped with the TypeFamily.FamilyOptic factory
                var arg = TypeFamily.FamilyOptic<A, B>(i => fo[0].Apply(i));
                fo.Add((FamilyOptic<A, B>)(object)_template.ApplyO(arg, aType, bType)!);
                var parts = fo[0].Apply(index);
                return Either<object, T.Type<object>.FieldNotFoundException>.Left(
                    (object)parts.CastOuterUnchecked((T.Type<object>)(object)sType, (T.Type<object>)(object)tType));
            }
            else
            {
                return MkSimpleOptic(sType, tType, aType, bType, matcher);
            }
        });
    }

    //mkSimpleOptic builds a simple optic directly in the non-recursive case
    private Either<object, T.Type<object>.FieldNotFoundException> MkSimpleOptic<S, T2, A, B>(
        RecursivePoint.RecursivePointType<S> sType, RecursivePoint.RecursivePointType<T2> tType,
        T.Type<A> aType, T.Type<B> bType, T.Type<object>.TypeMatcher<A, B> matcher)
    {
        return sType.Unfold().FindType(aType, bType, (T.Type<S>.TypeMatcher<A, B>)(object)matcher, false)
            .MapLeft(o => (object)((TypedOptic<object, object, A, B>)o!).CastOuterUnchecked(
                (T.Type<S>)(object)sType, (T.Type<T2>)(object)tType))
            .MapRight(fn => (T.Type<object>.FieldNotFoundException)(object)fn);
    }

    //everywhere applies the rule recursively everywhere
    public Optional<RewriteResult<object, object>> Everywhere(
        int index, object rule, PointFreeRule optimizationRule)
    {
        var sourceType = ((RecursivePoint.RecursivePointType<object>)Apply(index)).Unfold();
        var sourceView = DataFixUtils.OrElse(
            sourceType.Everywhere(rule, optimizationRule, false, false),
            RewriteResult<object, object>.Nop(sourceType));
        var newType = (RecursivePoint.RecursivePointType<object>)BuildMuType<object>(sourceView.View().NewType(), null)!;
        var newFamily = newType.Family();

        var views = new List<RewriteResult<object, object>>();
        bool foundAny = false;
        for (int i = 0; i < _size; i++)
        {
            var type = (RecursivePoint.RecursivePointType<object>)(object)Apply(i)!;
            var unfold = type.Unfold();
            bool nop1 = true;
            var view = DataFixUtils.OrElse(
                unfold.Everywhere(rule, optimizationRule, false, true),
                RewriteResult<object, object>.Nop(unfold));
            if (!view.View().IsNop())
            {
                nop1 = false;
            }

            var newMuType = (RecursivePoint.RecursivePointType<object>)BuildMuType<object>(view.View().NewType(), newFamily)!;
            bool nop = Cap2(views, type, rule, optimizationRule, nop1, view, newMuType);
            foundAny = foundAny || !nop;
        }
        if (!foundAny)
        {
            return Optional<RewriteResult<object, object>>.Empty();
        }
        var algebra = new ListAlgebra("everywhere", views);
        var fold = Fold(algebra, newFamily)(index);
        return Optional<RewriteResult<object, object>>.Of(
            RewriteResult<object, object>.Create(
                View<object, object>.Create(fold.View().Function!, fold.View().Type(), fold.View().NewType()),
                fold.RecData()));
    }

    //cap2 merges a single-index rewrite into the views list and returns whether it is nop
    private bool Cap2<A, B>(
        List<RewriteResult<object, object>> views,
        RecursivePoint.RecursivePointType<A> type,
        object rule, PointFreeRule optimizationRule,
        bool nop, RewriteResult<object, object> view,
        RecursivePoint.RecursivePointType<B> newType)
    {
        //view may actually be a subtype such as RewriteResult<Either<object,object>,object>
        //under strict generic invariance it cannot be cast to RewriteResult<A,B>; use Unsafe.As to bypass the runtime check
        var viewObj1 = (object)view;
        var viewAsAB = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<A, B>>(ref viewObj1);
        var newView = ComposeRewrite<A, B, B>(
            RewriteResult<B, B>.Create(newType.In(), new NetCraft.Util.BitSet()),
            viewAsAB);
        var rewrite = ((TypeRewriteRule)rule).Rewrite(newView.View().NewType());
        if (rewrite.IsPresent && !rewrite.Get().View().IsNop())
        {
            nop = false;
            //rewrite.Get() returns RewriteResult<object,object> but may actually be RewriteResult<B,B>
            //ComposeRewrite returns RewriteResult<A,B> but may actually be RewriteResult<object,object>
            //both places use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var rewriteObj = (object)rewrite.Get();
            var rewriteAsBB = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<B, B>>(ref rewriteObj);
            var composedObj = (object)ComposeRewrite<A, B, B>(rewriteAsBB, newView);
            view = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref composedObj);
        }
        view = RewriteResult<object, object>.Create(
            DataFixUtils.OrElse(view.View().Rewrite(optimizationRule), view.View()),
            view.RecData());
        views.Add(view);
        return nop;
    }

    //composeRewrite merges two RewriteResults, reusing View composition
    private static RewriteResult<C, B2> ComposeRewrite<C, A2, B2>(
        RewriteResult<A2, B2> first, RewriteResult<C, A2> second)
        => RewriteResult<C, B2>.Create(
            ComposeView<A2, C, B2>(first.View(), second.View()),
            first.RecData());

    //composeView composes two Views, handling nop short-circuiting
    //both PointFree/View casts use Unsafe.As to bypass C# strict generic invariance, aligning with Java type erasure
    private static View<C, B2> ComposeView<A2, C, B2>(View<A2, B2> first, View<C, A2> second)
    {
        if (first.IsNop())
        {
            var secondObj = (object)second;
            return System.Runtime.CompilerServices.Unsafe.As<object, View<C, B2>>(ref secondObj);
        }
        if (second.IsNop())
        {
            var firstObj = (object)first;
            return System.Runtime.CompilerServices.Unsafe.As<object, View<C, B2>>(ref firstObj);
        }
        var firstFuncObj = (object)first.Function!;
        var firstFunc = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<A2, C>>>(ref firstFuncObj);
        var secondFuncObj = (object)second.Function!;
        var secondFunc = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<B2, A2>>>(ref secondFuncObj);
        var composed = Functions.Comp<B2, A2, C>(firstFunc, secondFunc);
        var composedObj = (object)composed;
        var composedCast = System.Runtime.CompilerServices.Unsafe.As<object, PointFree<Func<C, B2>>>(ref composedObj);
        var viewObj = (object)View<C, B2>.Create(composedCast, second.OldTypeValue, first.NewTypeValue);
        return System.Runtime.CompilerServices.Unsafe.As<object, View<C, B2>>(ref viewObj);
    }

    public override string ToString() => "Mu[" + _name + ", " + _size + ", " + _template + "]";

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not RecursiveTypeFamily family) return false;
        return ReferenceEquals(_template, family._template);
    }

    public override int GetHashCode() => _hashCode;
}
