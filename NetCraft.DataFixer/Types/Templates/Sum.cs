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

//Sum和类型模板对应原版com.mojang.datafixers.types.templates.Sum
//表示两个模板的和Either<F,G>
public sealed record Sum(TypeTemplate F, TypeTemplate G) : TypeTemplate
{
    public int Size() => Math.Max(F.Size(), G.Size());

    //apply每个index返回DSL.or(f, g)包装
    public TypeFamily Apply(TypeFamily family)
        => new SumFamily(this, family);

    //applyO合并两侧元素applyO的结果
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)CapOptic<A, B>(
            (FamilyOptic<A, B>)(object)F.ApplyO(input, aType, bType),
            (FamilyOptic<A, B>)(object)G.ApplyO(input, aType, bType),
            i));

    //CapOptic合并两侧TypedOptic为Either上的合并optic
    //原版Java用类型擦除让LS/RS/LT/RT为通配C#用object强转消除类型参数
    private static TypedOptic<object, object, A, B> CapOptic<A, B>(
        FamilyOptic<A, B> lo, FamilyOptic<A, B> ro, int index)
        => (TypedOptic<object, object, A, B>)(object)SumType<object, object>.MergeOptics<A, B>(
            lo.Apply(index), ro.Apply(index));

    //findFieldOrType先在f中查找失败再在g中查找后用Sum包装
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        var either = F.FindFieldOrType(index, name, type, resultType);
        if (either.IsLeft)
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                .Left(new Sum(either.GetLeft().Get(), G));
        }
        var either2 = G.FindFieldOrType(index, name, type, resultType);
        if (either2.IsLeft)
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                .Left(new Sum(F, either2.GetLeft().Get()));
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

    //CapView两侧元素重写结果用SumType.mergeViews合并
    private static RewriteResult<object, object> CapView<L, R>(T.Type<object> type, RewriteResult<L, object> f1, RewriteResult<R, object> f2)
        => (RewriteResult<object, object>)(object)((SumType<L, R>)(object)type).MergeViews(f1, f2);

    public override string ToString() => "(" + F + " | " + G + ")";

    //SumFamily按index返回DSL.or包装的子类型
    private sealed class SumFamily : TypeFamily
    {
        private readonly Sum _template;
        private readonly TypeFamily _family;
        public SumFamily(Sum template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
        {
            //F/G.Apply返回T.Type<A>包装为Either<F,G>后强转T.Type<object>会失败
            //三处都用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var fObj = (object)_template.F.Apply(_family).Apply(index)!;
            var fType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
            var gObj = (object)_template.G.Apply(_family).Apply(index)!;
            var gType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref gObj);
            var sumType = DSL.Or(fType, gType);
            var sumObj = (object)sumType;
            return System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sumObj);
        }
    }

    //SumType和类型持有两个子类型对应Either<F,G>
    public sealed class SumType<F, G> : T.Type<Either<F, G>>
    {
        private readonly T.Type<F> _first;
        private readonly T.Type<G> _second;
        private int _hashCode;

        public SumType(T.Type<F> first, T.Type<G> second)
        {
            _first = first;
            _second = second;
        }

        public T.Type<F> First() => _first;
        public T.Type<G> Second() => _second;

        //all对两侧元素应用规则后mergeViews合并
        public override RewriteResult<Either<F, G>, object> All(object rule, bool recurse, bool checkIndex)
            => MergeViews(_first.RewriteOrNop(rule), _second.RewriteOrNop(rule));

        //mergeViews分两步先fixLeft再fixRight后compose
        //类型擦除后Compose类型不匹配用object强转对齐原版Java语义
        //v2.Compose返回RewriteResult<Either<F,G>,object>但v1的cast为RewriteResult<object,Either<F,G>>严格不变量下失败
        //两处cast都用Unsafe.As绕过运行时类型检查
        public RewriteResult<Either<F, G>, object> MergeViews(
            RewriteResult<F, object> leftView, RewriteResult<G, object> rightView)
        {
            var v1 = FixLeft(this, _first, _second, leftView);
            var v2 = FixRight((T.Type<Either<F, G>>)(object)v1.View().NewType()!, _first, _second, rightView);
            var v1Obj = (object)v1;
            var v1Casted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, Either<F, G>>>(ref v1Obj);
            var composed = v2.Compose(v1Casted);
            var composedObj = (object)composed;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<Either<F, G>, object>>(ref composedObj);
        }

        //one先尝试first再尝试second任意命中即返回
        public override Optional<RewriteResult<Either<F, G>, object>> One(object rule)
        {
            var firstOpt = ((TypeRewriteRule)rule).Rewrite(_first);
            if (firstOpt.IsPresent)
            {
                return Optional<RewriteResult<Either<F, G>, object>>.Of(
                    FixLeft(this, _first, _second, (RewriteResult<F, object>)firstOpt.Get()));
            }
            var secondOpt = ((TypeRewriteRule)rule).Rewrite(_second);
            if (secondOpt.IsPresent)
            {
                return Optional<RewriteResult<Either<F, G>, object>>.Of(
                    FixRight(this, _first, _second, (RewriteResult<G, object>)secondOpt.Get()));
            }
            return Optional<RewriteResult<Either<F, G>, object>>.Empty();
        }

        //findFieldTypeOpt先在first中查失败再在second中查
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
        {
            var firstOpt = _first.FindFieldTypeOpt(name);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindFieldTypeOpt(name);
        }

        //findChoiceType委托first失败再委托second对应原版SumType.findChoiceType
        public override Optional<object> FindChoiceType(string name, int index)
        {
            var firstOpt = _first.FindChoiceType(name, index);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindChoiceType(name, index);
        }

        //findCheckedType委托first失败再委托second对应原版SumType.findCheckedType
        public override Optional<T.Type<object>> FindCheckedType(int index)
        {
            var firstOpt = _first.FindCheckedType(index);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindCheckedType(index);
        }

        //fixLeft把first侧重写结果用inj1投射到Either层
        //view实际是RewriteResult<F,object>不变量下不能cast为RewriteResult<object,object>
        //Inj1返回TypedOptic<...F,object>也不能直接cast到<...object,object>
        //两处都用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
        private static RewriteResult<Either<F, G>, object> FixLeft(
            T.Type<Either<F, G>> type, T.Type<F> first, T.Type<G> second, RewriteResult<F, object> view)
        {
            var viewObj = (object)view;
            var viewAsObject = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            //NewType编译时Type<object>运行时可能是ListType<object>继承Type<List<object>>不继承Type<object>
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
            var newTypeObj = (object)view.View().NewType()!;
            var newType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref newTypeObj);
            var optic = TypedOptics.Inj1<F, G, object>(first, second, newType)!;
            var opticObj = (object)optic;
            var opticCasted = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<Either<F, G>, Either<object, G>, object, object>>(ref opticObj);
            var opticViewObj = (object)T.Type<Either<F, G>>.OpticView(type, viewAsObject, opticCasted);
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<Either<F, G>, object>>(ref opticViewObj);
        }

        //fixRight把second侧重写结果用inj2投射到Either层
        //同FixLeft用Unsafe.As绕过view和optic两处cast
        private static RewriteResult<Either<F, G>, object> FixRight(
            T.Type<Either<F, G>> type, T.Type<F> first, T.Type<G> second, RewriteResult<G, object> view)
        {
            var viewObj = (object)view;
            var viewAsObject = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            var newTypeObj = (object)view.View().NewType()!;
            var newType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref newTypeObj);
            var optic = TypedOptics.Inj2<F, G, object>(first, second, newType)!;
            var opticObj = (object)optic;
            var opticCasted = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<Either<F, G>, Either<F, object>, object, object>>(ref opticObj);
            var opticViewObj = (object)T.Type<Either<F, G>>.OpticView(type, viewAsObject, opticCasted);
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<Either<F, G>, object>>(ref opticViewObj);
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.Or(_first.UpdateMu(newFamily), _second.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.Or(_first.Template(), _second.Template());

        protected override Codec<Either<F, G>> BuildCodec()
            => new SumCodec(this);

        //SumCodec和类型codec按Either分支委托first或second codec
        private sealed class SumCodec : ScalarCodec<Either<F, G>>
        {
            private readonly SumType<F, G> _type;
            public SumCodec(SumType<F, G> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Either<F, G> value)
                => value.Map(
                    l => TypeObjectConverterFactory.AsObjectType(_type.First()).Codec().EncodeStart(ops, l),
                    r => TypeObjectConverterFactory.AsObjectType(_type.Second()).Codec().EncodeStart(ops, r));

            //parse两侧尝试委托first失败用second对应原版SumType.parse
            //_first/_second可能是Unsafe.As包装的非Type<F>实例直接Codec返回的codec方法表不匹配
            //用AsObjectType包装后Codec返回CodecAdapter真正Codec<object>避免EntryPointNotFoundException
            public override DataResult<Either<F, G>> Parse<U>(DynamicOps<U> ops, U input)
            {
                var firstWrapped = TypeObjectConverterFactory.AsObjectType(_type._first);
                var firstCodec = firstWrapped.Codec();
                var firstResultObj = firstCodec.Parse(ops, input);
                var firstResult = (DataResult<F>)(object)firstResultObj;
                if (firstResult.Result().IsPresent)
                {
                    return firstResult.Map(Either<F, G>.Left);
                }
                var secondWrapped = TypeObjectConverterFactory.AsObjectType(_type._second);
                var secondCodec = secondWrapped.Codec();
                var secondResultObj = secondCodec.Parse(ops, input);
                var secondResult = (DataResult<G>)(object)secondResultObj;
                return secondResult.Map(Either<F, G>.Right);
            }
        }

        public override Optional<Either<F, G>> Point<T>(DynamicOps<T> ops)
        {
            //反序尝试优先取最少嵌套项
            var secondPoint = _second.Point(ops);
            if (secondPoint.IsPresent)
            {
                return Optional<Either<F, G>>.Of(Either<F, G>.Right(secondPoint.Get()));
            }
            var firstPoint = _first.Point(ops);
            if (firstPoint.IsPresent)
            {
                return Optional<Either<F, G>>.Of(Either<F, G>.Left(firstPoint.Get()));
            }
            return Optional<Either<F, G>>.Empty();
        }

        //mergeOptics合并两侧TypedOptic为Either上的TraversalP optic
        //两侧optic统一为TypedOptic<object,object,A,B>简化原版泛型签名
        public static TypedOptic<Either<object, object>, Either<object, object>, A, B> MergeOptics<A, B>(
            TypedOptic<object, object, A, B> lo, TypedOptic<object, object, A, B> ro)
            => new TypedOptic<Either<object, object>, Either<object, object>, A, B>(
                TypeClassesMarker.TraversalPToken,
                (T.Type<Either<object, object>>)(object)DSL.Or(lo.SType(), ro.SType())!,
                (T.Type<Either<object, object>>)(object)DSL.Or(lo.TType(), ro.TType())!,
                lo.AType(),
                lo.BType(),
                OpticsClass.EitherTraversal<object, object, object, object, A, B>(
                    OpticsClass.ToTraversal<object, object, A, B>(
                        (Optic<ITraversalPMu, object, object, A, B>)(object)lo.UpCast(TypeClassesMarker.TraversalPToken)!.Get())!,
                    OpticsClass.ToTraversal<object, object, A, B>(
                        (Optic<ITraversalPMu, object, object, A, B>)(object)ro.UpCast(TypeClassesMarker.TraversalPToken)!.Get())!));

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (o is not SumType<F, G> that) return false;
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
}
