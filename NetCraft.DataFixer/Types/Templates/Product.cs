namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Product积类型模板对应原版com.mojang.datafixers.types.templates.Product
//表示两个模板的积Pair<F,G>
public sealed record Product(TypeTemplate F, TypeTemplate G) : TypeTemplate
{
    public int Size() => Math.Max(F.Size(), G.Size());

    //apply每个index返回DSL.and(f, g)包装
    public TypeFamily Apply(TypeFamily family)
        => new ProductFamily(this, family);

    //applyO合并两侧元素applyO的结果用TraversalP组合
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)CapOptic<A, B>(
            (FamilyOptic<A, B>)(object)F.ApplyO(input, aType, bType),
            (FamilyOptic<A, B>)(object)G.ApplyO(input, aType, bType),
            i));

    //CapOptic合并两侧TypedOptic为Pair上的TraversalP合并optic
    //原版Java用类型擦除让LS/RS/LT/RT为通配C#用object强转消除类型参数
    private static TypedOptic<object, object, A, B> CapOptic<A, B>(
        FamilyOptic<A, B> lo, FamilyOptic<A, B> ro, int index)
    {
        var lp = lo.Apply(index);
        var rp = ro.Apply(index);
        return new TypedOptic<object, object, A, B>(
            TypeClassesMarker.TraversalPToken,
            (T.Type<object>)(object)DSL.And(lp.SType(), rp.SType())!,
            (T.Type<object>)(object)DSL.And(lp.TType(), rp.TType())!,
            lp.AType(),
            lp.BType(),
            BuildProductTraversal<A, B>(lp, rp));
    }

    //BuildProductTraversal构造Pair上的Traversal合并两侧焦点
    //两侧optic强转为Traversal<object,object,A,B>简化原版wander逻辑
    private static object BuildProductTraversal<A, B>(
        TypedOptic<object, object, A, B> lo, TypedOptic<object, object, A, B> ro)
    {
        var lTraversal = OpticsClass.ToTraversal<object, object, A, B>(
            (Optic<ITraversalPMu, object, object, A, B>)(object)lo.UpCast(TypeClassesMarker.TraversalPToken)!.Get())!;
        var rTraversal = OpticsClass.ToTraversal<object, object, A, B>(
            (Optic<ITraversalPMu, object, object, A, B>)(object)ro.UpCast(TypeClassesMarker.TraversalPToken)!.Get())!;
        return new ProductTraversal<A, B>(lTraversal, rTraversal);
    }

    //findFieldOrType先在f中查找失败再在g中查找后用Product包装
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        var either = F.FindFieldOrType(index, name, type, resultType);
        if (either.IsLeft)
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                .Left(new Product(either.GetLeft().Get(), G));
        }
        var either2 = G.FindFieldOrType(index, name, type, resultType);
        if (either2.IsLeft)
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                .Left(new Product(F, either2.GetLeft().Get()));
        }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Right(either2.GetRight().Get());
    }

    //hmap对两侧元素应用hmap后用cap合并
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => i =>
        {
            var f1 = F.Hmap(family, function)(i);
            var f2 = G.Hmap(family, function)(i);
            return CapView(Apply(family).Apply(i), f1, f2);
        };

    //CapView两侧元素重写结果用ProductType.mergeViews合并
    private static RewriteResult<object, object> CapView<L, R>(T.Type<object> type, RewriteResult<L, object> f1, RewriteResult<R, object> f2)
        => (RewriteResult<object, object>)(object)((ProductType<L, R>)(object)type).MergeViews(f1, f2);

    public override string ToString() => "(" + F + ", " + G + ")";

    //ProductFamily按index返回DSL.and包装的子类型
    private sealed class ProductFamily : TypeFamily
    {
        private readonly Product _template;
        private readonly TypeFamily _family;
        public ProductFamily(Product template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
        {
            //F/G.Apply返回T.Type<A>包装为Pair<F,G>后强转T.Type<object>会失败
            //三处都用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var fObj = (object)_template.F.Apply(_family).Apply(index)!;
            var fType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
            var gObj = (object)_template.G.Apply(_family).Apply(index)!;
            var gType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref gObj);
            var productType = DSL.And(fType, gType);
            var productObj = (object)productType;
            return System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref productObj);
        }
    }

    //ProductType积类型持有两个子类型对应Pair<F,G>
    public sealed class ProductType<F, G> : T.Type<NetCraft.DataFixer.Util.Pair<F, G>>
    {
        private readonly T.Type<F> _first;
        private readonly T.Type<G> _second;
        private int _hashCode;

        public ProductType(T.Type<F> first, T.Type<G> second)
        {
            _first = first;
            _second = second;
        }

        public T.Type<F> First() => _first;
        public T.Type<G> Second() => _second;

        //all对两侧元素应用规则后mergeViews合并
        public override RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object> All(object rule, bool recurse, bool checkIndex)
            => MergeViews(_first.RewriteOrNop(rule), _second.RewriteOrNop(rule));

        //mergeViews分两步先fixLeft再fixRight后compose
        //类型擦除后Compose类型不匹配用object强转对齐原版Java语义
        //v1.NewType与v1/v2/composed的强转都用Unsafe.As绕过C#严格泛型不变量
        public RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object> MergeViews(
            RewriteResult<F, object> leftView, RewriteResult<G, object> rightView)
        {
            var v1 = FixLeft(this, _first, _second, leftView);
            var newTypeObj = (object)v1.View().NewType()!;
            var newType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<NetCraft.DataFixer.Util.Pair<F, G>>>(ref newTypeObj);
            var v2 = FixRight(newType, _first, _second, rightView);
            var v1Obj = (object)v1;
            var v1Casted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, NetCraft.DataFixer.Util.Pair<F, G>>>(ref v1Obj);
            var composedObj = (object)v2.Compose(v1Casted);
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object>>(ref composedObj);
        }

        //one先尝试first再尝试second任意命中即返回
        public override Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object>> One(object rule)
        {
            var firstOpt = ((TypeRewriteRule)rule).Rewrite(_first);
            if (firstOpt.IsPresent)
            {
                return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object>>.Of(
                    FixLeft(this, _first, _second, (RewriteResult<F, object>)firstOpt.Get()));
            }
            var secondOpt = ((TypeRewriteRule)rule).Rewrite(_second);
            if (secondOpt.IsPresent)
            {
                return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object>>.Of(
                    FixRight(this, _first, _second, (RewriteResult<G, object>)secondOpt.Get()));
            }
            return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object>>.Empty();
        }

        //findFieldTypeOpt先在first中查失败再在second中查
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
        {
            var firstOpt = _first.FindFieldTypeOpt(name);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindFieldTypeOpt(name);
        }

        //fixLeft把first侧重写结果用proj1投射到Pair层
        //view与TypedOptic的强转用Unsafe.As绕过C#严格泛型不变量对齐Java类型擦除
        private static RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object> FixLeft(
            T.Type<NetCraft.DataFixer.Util.Pair<F, G>> type, T.Type<F> first, T.Type<G> second, RewriteResult<F, object> view)
        {
            var viewObj = (object)view;
            var viewCast = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            var newTypeObj = (object)view.View().NewType()!;
            var newType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref newTypeObj);
            var optic = TypedOptics.Proj1<F, G, object>(first, second, newType)!;
            var opticObj = (object)optic;
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<NetCraft.DataFixer.Util.Pair<F, G>, object, object, object>>(ref opticObj);
            return T.Type<NetCraft.DataFixer.Util.Pair<F, G>>.OpticView(type, viewCast, opticCast);
        }

        //fixRight把second侧重写结果用proj2投射到Pair层
        //同FixLeft用Unsafe.As绕过view和optic两处cast
        private static RewriteResult<NetCraft.DataFixer.Util.Pair<F, G>, object> FixRight(
            T.Type<NetCraft.DataFixer.Util.Pair<F, G>> type, T.Type<F> first, T.Type<G> second, RewriteResult<G, object> view)
        {
            var viewObj = (object)view;
            var viewCast = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            var newTypeObj = (object)view.View().NewType()!;
            var newType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref newTypeObj);
            var optic = TypedOptics.Proj2<F, G, object>(first, second, newType)!;
            var opticObj = (object)optic;
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<NetCraft.DataFixer.Util.Pair<F, G>, object, object, object>>(ref opticObj);
            return T.Type<NetCraft.DataFixer.Util.Pair<F, G>>.OpticView(type, viewCast, opticCast);
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.And(_first.UpdateMu(newFamily), _second.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.And(_first.Template(), _second.Template());

        protected override Codec<NetCraft.DataFixer.Util.Pair<F, G>> BuildCodec()
            => new ProductCodec(this);

        //ProductCodec积类型codec用pair组合first与second
        private sealed class ProductCodec : ScalarCodec<NetCraft.DataFixer.Util.Pair<F, G>>
        {
            private readonly ProductType<F, G> _type;
            public ProductCodec(ProductType<F, G> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.DataFixer.Util.Pair<F, G> value)
            {
                var firstEncoded = _type._first.Codec().EncodeStart(ops, value.First);
                if (!firstEncoded.Result().IsPresent) return DataResult<U>.Error(() => "first encode failed");
                var secondEncoded = _type._second.Codec().EncodeStart(ops, value.Second);
                if (!secondEncoded.Result().IsPresent) return DataResult<U>.Error(() => "second encode failed");
                //合并两个值这里用list包装简化原版用Codec.pair的实现
                return DataResult<U>.Success(ops.MergeToList(firstEncoded.GetOrThrow(), secondEncoded.GetOrThrow()).GetOrThrow());
            }

            public override DataResult<NetCraft.DataFixer.Util.Pair<F, G>> Parse<U>(DynamicOps<U> ops, U input)
            {
                return ops.GetStream(input).Map(stream =>
                {
                    var list = stream.ToList();
                    if (list.Count < 2)
                    {
                        return NetCraft.DataFixer.Util.Pair<F, G>.Of(
                            _type._first.Codec().Parse(ops, list.Count > 0 ? list[0] : ops.Empty()).GetOrThrow(),
                            default!);
                    }
                    var first = _type._first.Codec().Parse(ops, list[0]).GetOrThrow();
                    var second = _type._second.Codec().Parse(ops, list[1]).GetOrThrow();
                    return NetCraft.DataFixer.Util.Pair<F, G>.Of(first, second);
                });
            }
        }

        public override Optional<NetCraft.DataFixer.Util.Pair<F, G>> Point<T>(DynamicOps<T> ops)
        {
            var firstPoint = _first.Point(ops);
            if (!firstPoint.IsPresent) return Optional<NetCraft.DataFixer.Util.Pair<F, G>>.Empty();
            var secondPoint = _second.Point(ops);
            if (!secondPoint.IsPresent) return Optional<NetCraft.DataFixer.Util.Pair<F, G>>.Empty();
            return Optional<NetCraft.DataFixer.Util.Pair<F, G>>.Of(NetCraft.DataFixer.Util.Pair<F, G>.Of(firstPoint.Get(), secondPoint.Get()));
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (o is not ProductType<F, G> that) return false;
            return _first.Equals(that._first, ignoreRecursionPoints, checkIndex)
                && _second.Equals(that._second, ignoreRecursionPoints, checkIndex);
        }

        public override int GetHashCode()
        {
            if (_hashCode == 0)
            {
                _hashCode = unchecked((_first?.GetHashCode() ?? 0) * 31 + (_second?.GetHashCode() ?? 0));
            }
            return _hashCode;
        }
    }

    //ProductTraversal积类型遍历用两侧Traversal分别处理Pair的first与second
    //两侧Traversal统一为Traversal<object,object,A,B>简化原版泛型签名
    private sealed class ProductTraversal<A, B> : Traversal<NetCraft.DataFixer.Util.Pair<object, object>, NetCraft.DataFixer.Util.Pair<object, object>, A, B>
    {
        private readonly Traversal<object, object, A, B> _left;
        private readonly Traversal<object, object, A, B> _right;
        public ProductTraversal(Traversal<object, object, A, B> left, Traversal<object, object, A, B> right)
        {
            _left = left;
            _right = right;
        }

        public Func<NetCraft.DataFixer.Util.Pair<object, object>, App<F2, NetCraft.DataFixer.Util.Pair<object, object>>> Wander<F2, TMu2>(Applicative<F2, TMu2> applicative, Func<A, App<F2, B>> input)
            where F2 : K1 where TMu2 : IApplicativeMu
        {
            return pair =>
            {
                var leftResult = _left.Wander(applicative, input).Invoke(pair.First);
                var rightResult = _right.Wander(applicative, input).Invoke(pair.Second);
                //Apply2合并两侧结果为Pair对应原版applicative.ap2(point(Pair::of), ...)
                return applicative.Apply2(
                    (Func<object, object, NetCraft.DataFixer.Util.Pair<object, object>>)((l, r) => NetCraft.DataFixer.Util.Pair<object, object>.Of(l, r)),
                    leftResult, rightResult);
            };
        }
    }
}
