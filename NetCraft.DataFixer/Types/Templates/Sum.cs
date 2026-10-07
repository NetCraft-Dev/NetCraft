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

//Sum sum type template maps to vanilla com.mojang.datafixers.types.templates.Sum
//represents the sum of two templates, Either<F,G>
public sealed record Sum(TypeTemplate F, TypeTemplate G) : TypeTemplate
{
    public int Size() => Math.Max(F.Size(), G.Size());

    //apply returns DSL.or(f, g)-wrapped types at each index
    public TypeFamily Apply(TypeFamily family)
        => new SumFamily(this, family);

    //applyO merges the results of both elements' applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)CapOptic<A, B>(
            (FamilyOptic<A, B>)(object)F.ApplyO(input, aType, bType),
            (FamilyOptic<A, B>)(object)G.ApplyO(input, aType, bType),
            i));

    //CapOptic merges both TypedOptics into a merged optic on Either
    //vanilla Java uses type erasure to make LS/RS/LT/RT wildcards; C# uses object casts to erase the type parameters
    private static TypedOptic<object, object, A, B> CapOptic<A, B>(
        FamilyOptic<A, B> lo, FamilyOptic<A, B> ro, int index)
        => (TypedOptic<object, object, A, B>)(object)SumType<object, object>.MergeOptics<A, B>(
            lo.Apply(index), ro.Apply(index));

    //findFieldOrType searches f first, then g on failure, then wraps with Sum
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

    //hmap applies hmap to both elements, then merges with cap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => i =>
        {
            var f1 = F.Hmap(family, function)(i);
            var f2 = G.Hmap(family, function)(i);
            return CapView(Apply(family).Apply(i), f1, f2);
        };

    //CapView merges the rewrite results of both elements via SumType.mergeViews
    private static RewriteResult<object, object> CapView<L, R>(T.Type<object> type, RewriteResult<L, object> f1, RewriteResult<R, object> f2)
        => (RewriteResult<object, object>)(object)((SumType<L, R>)(object)type).MergeViews(f1, f2);

    public override string ToString() => "(" + F + " | " + G + ")";

    //SumFamily returns the child type wrapped with DSL.or at each index
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
            //F/G.Apply returns T.Type<A>; casting to T.Type<object> after wrapping as Either<F,G> fails
            //all three places use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var fObj = (object)_template.F.Apply(_family).Apply(index)!;
            var fType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref fObj);
            var gObj = (object)_template.G.Apply(_family).Apply(index)!;
            var gType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref gObj);
            var sumType = DSL.Or(fType, gType);
            var sumObj = (object)sumType;
            return System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sumObj);
        }
    }

    //SumType sum type holding two child types, maps to Either<F,G>
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

        //all applies the rule to both elements, then merges via mergeViews
        public override RewriteResult<Either<F, G>, object> All(object rule, bool recurse, bool checkIndex)
            => MergeViews(_first.RewriteOrNop(rule), _second.RewriteOrNop(rule));

        //mergeViews works in two steps: fixLeft then fixRight, then compose
        //after type erasure Compose types do not match; cast via object to align with vanilla Java semantics
        //v2.Compose returns RewriteResult<Either<F,G>,object> but v1's cast is RewriteResult<object,Either<F,G>>, failing under strict invariance
        //both casts use Unsafe.As to bypass the runtime type check
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

        //one tries first then second; returns on either match
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

        //findFieldTypeOpt searches first then second on failure
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
        {
            var firstOpt = _first.FindFieldTypeOpt(name);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindFieldTypeOpt(name);
        }

        //findChoiceType delegates to first then second on failure, maps to vanilla SumType.findChoiceType
        public override Optional<object> FindChoiceType(string name, int index)
        {
            var firstOpt = _first.FindChoiceType(name, index);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindChoiceType(name, index);
        }

        //findCheckedType delegates to first then second on failure, maps to vanilla SumType.findCheckedType
        public override Optional<T.Type<object>> FindCheckedType(int index)
        {
            var firstOpt = _first.FindCheckedType(index);
            if (firstOpt.IsPresent) return firstOpt;
            return _second.FindCheckedType(index);
        }

        //fixLeft projects the first-side rewrite result into the Either layer via inj1
        //view is actually RewriteResult<F,object> and cannot be cast to RewriteResult<object,object> under invariance
        //Inj1 returns TypedOptic<...F,object>, which also cannot be cast directly to <...object,object>
        //both places use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
        private static RewriteResult<Either<F, G>, object> FixLeft(
            T.Type<Either<F, G>> type, T.Type<F> first, T.Type<G> second, RewriteResult<F, object> view)
        {
            var viewObj = (object)view;
            var viewAsObject = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            //NewType is Type<object> at compile time but may be ListType<object> at runtime, inheriting Type<List<object>> not Type<object>
            //use Unsafe.As to bypass the runtime type check and align with Java type erasure
            var newTypeObj = (object)view.View().NewType()!;
            var newType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref newTypeObj);
            var optic = TypedOptics.Inj1<F, G, object>(first, second, newType)!;
            var opticObj = (object)optic;
            var opticCasted = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<Either<F, G>, Either<object, G>, object, object>>(ref opticObj);
            var opticViewObj = (object)T.Type<Either<F, G>>.OpticView(type, viewAsObject, opticCasted);
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<Either<F, G>, object>>(ref opticViewObj);
        }

        //fixRight projects the second-side rewrite result into the Either layer via inj2
        //same as FixLeft: use Unsafe.As to bypass both the view and optic casts
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

        //SumCodec sum type codec; delegates to first or second codec by Either branch
        private sealed class SumCodec : ScalarCodec<Either<F, G>>
        {
            private readonly SumType<F, G> _type;
            public SumCodec(SumType<F, G> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Either<F, G> value)
                => value.Map(
                    l => TypeObjectConverterFactory.AsObjectType(_type.First()).Codec().EncodeStart(ops, l),
                    r => TypeObjectConverterFactory.AsObjectType(_type.Second()).Codec().EncodeStart(ops, r));

            //parse tries both sides; delegates to first, then second on failure, maps to vanilla SumType.parse
            //_first/_second may be non-Type<F> instances wrapped by Unsafe.As, so the codec returned by calling Codec directly has a mismatched method table
            //wrap with AsObjectType so Codec returns a CodecAdapter that is truly Codec<object>, avoiding EntryPointNotFoundException
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
            //tries in reverse order, preferring the least nested item
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

        //mergeOptics merges both TypedOptics into a TraversalP optic on Either
        //both optics are unified as TypedOptic<object,object,A,B>, simplifying the vanilla generic signature
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
