namespace NetCraft.DataFixer.Types.Families;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//RecursiveTypeFamily递归类型家族对应原版RecursiveTypeFamily
//由模板构建按index返回RecursivePointType用于递归类型
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

    //buildMuType根据新类型查找或构造对应家族
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

    //fold按代数产生index到RewriteResult的函数
    public Func<int, RewriteResult<object, object>> Fold(Algebra algebra, RecursiveTypeFamily newFamily)
        => index =>
        {
            var result = algebra.Apply(index);
            var func = FoldUnchecked<object, object>(this, newFamily, algebra, index);
            //对齐原版View.create(fold)只传function
            //type/newType从func.type()推导为RecursivePointType而非result.view里的CheckType
            var funcType = (NetCraft.DataFixer.Types.Func<object, object>)func.Type()!;
            return RewriteResult<object, object>.Create(
                View<object, object>.Create(
                    func,
                    funcType.First(),
                    funcType.Second()),
                result.RecData());
        };

    //foldUnchecked按index取家族两端类型构造折叠PointFree
    private static PointFree<Func<A, B>> FoldUnchecked<A, B>(
        RecursiveTypeFamily family, RecursiveTypeFamily newFamily, Algebra algebra, int index)
    {
        var type = (RecursivePoint.RecursivePointType<A>)(object)family.Apply(index)!;
        var newType = (RecursivePoint.RecursivePointType<B>)(object)newFamily.Apply(index)!;
        return Functions.Fold<A, B>(type, newType, algebra, index);
    }

    //apply按索引返回RecursivePointType缓存构造
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

    //findType在指定索引处查找子类型optic
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
                //FamilyOptic是类非委托lambda需用TypeFamily.FamilyOptic工厂包装
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

    //mkSimpleOptic非递归情况下直接构造简单optic
    private Either<object, T.Type<object>.FieldNotFoundException> MkSimpleOptic<S, T2, A, B>(
        RecursivePoint.RecursivePointType<S> sType, RecursivePoint.RecursivePointType<T2> tType,
        T.Type<A> aType, T.Type<B> bType, T.Type<object>.TypeMatcher<A, B> matcher)
    {
        return sType.Unfold().FindType(aType, bType, (T.Type<S>.TypeMatcher<A, B>)(object)matcher, false)
            .MapLeft(o => (object)((TypedOptic<object, object, A, B>)o!).CastOuterUnchecked(
                (T.Type<S>)(object)sType, (T.Type<T2>)(object)tType))
            .MapRight(fn => (T.Type<object>.FieldNotFoundException)(object)fn);
    }

    //everywhere递归到处应用规则
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

    //cap2合并单index重写到views列表返回是否nop
    private bool Cap2<A, B>(
        List<RewriteResult<object, object>> views,
        RecursivePoint.RecursivePointType<A> type,
        object rule, PointFreeRule optimizationRule,
        bool nop, RewriteResult<object, object> view,
        RecursivePoint.RecursivePointType<B> newType)
    {
        //view实际可能是RewriteResult<Either<object,object>,object>等子类型
        //严格泛型不变量下不能cast为RewriteResult<A,B>用Unsafe.As绕过运行时检查
        var viewObj1 = (object)view;
        var viewAsAB = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<A, B>>(ref viewObj1);
        var newView = ComposeRewrite<A, B, B>(
            RewriteResult<B, B>.Create(newType.In(), new NetCraft.Util.BitSet()),
            viewAsAB);
        var rewrite = ((TypeRewriteRule)rule).Rewrite(newView.View().NewType());
        if (rewrite.IsPresent && !rewrite.Get().View().IsNop())
        {
            nop = false;
            //rewrite.Get()返回RewriteResult<object,object>实际可能是RewriteResult<B,B>
            //ComposeRewrite返回RewriteResult<A,B>实际可能是RewriteResult<object,object>
            //两处都用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
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

    //composeRewrite合并两个RewriteResult复用View复合
    private static RewriteResult<C, B2> ComposeRewrite<C, A2, B2>(
        RewriteResult<A2, B2> first, RewriteResult<C, A2> second)
        => RewriteResult<C, B2>.Create(
            ComposeView<A2, C, B2>(first.View(), second.View()),
            first.RecData());

    //composeView复合两个View处理nop短路
    //PointFree/View强转都用Unsafe.As绕过C#严格泛型不变量对齐Java类型擦除
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
