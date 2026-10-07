namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Check check template maps to vanilla com.mojang.datafixers.types.templates.Check
//uses the element template only when the index matches, for version checks
public sealed record Check(string Name, int Index, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Math.Max(Index + 1, Element.Size());

    //apply builds a CheckType wrapping the element type at each index
    public TypeFamily Apply(TypeFamily family)
        => new CheckFamily(this, family);

    //applyO directly reuses the element's applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType delegates to the element lookup only when the index matches
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        if (index == Index)
        {
            return Element.FindFieldOrType(index, name, type, resultType);
        }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
            .Right(new T.Type<object>.FieldNotFoundException("Not a matching index"));
    }

    //hmap applies hmap to the element at each index, then wraps it as a Check via cap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var elementResult = Element.Hmap(family, function)(index);
            return Cap(family, index, elementResult);
        };

    //Cap wraps the element rewrite result into a Check layer via CheckType.fix
    //A may be a complex type such as Pair<string,object>; casting to RewriteResult<object,object> fails
    //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
    private RewriteResult<object, object> Cap<A>(TypeFamily family, int index, RewriteResult<A, object> elementResult)
    {
        var fixResult = CheckType<A>.Fix((CheckType<A>)(object)Apply(family).Apply(index)!, elementResult);
        var fixObj = (object)fixResult;
        return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref fixObj);
    }

    public override string ToString() => "Tag[" + Name + ", " + Index + ": " + Element + "]";

    //CheckFamily returns the child type wrapped with CheckType at each index
    private sealed class CheckFamily : TypeFamily
    {
        private readonly Check _template;
        private readonly TypeFamily _family;
        public CheckFamily(Check template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
        {
            if (index < 0) throw new IndexOutOfRangeException();
            return (T.Type<object>)(object)new CheckType<object>(
                _template.Name,
                index,
                _template.Index,
                (T.Type<object>)(object)_template.Element.Apply(_family).Apply(index)!);
        }
    }

    //CheckType wrapper type with an index check
    public sealed class CheckType<A> : T.Type<A>
    {
        private readonly string _name;
        private readonly int _index;
        private readonly int _expectedIndex;
        private readonly T.Type<A> _delegate;

        public CheckType(string name, int index, int expectedIndex, T.Type<A> @delegate)
        {
            _name = name;
            _index = index;
            _expectedIndex = expectedIndex;
            _delegate = @delegate;
        }

        //buildCodec aligns with vanilla: uses delegate.codec with an index check on decode
        protected override Codec<A> BuildCodec()
            => new CheckCodec(this);

        //CheckCodec check codec; decode verifies the index matches then delegates
        private sealed class CheckCodec : ScalarCodec<A>
        {
            private readonly CheckType<A> _type;
            public CheckCodec(CheckType<A> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, A value)
                => _type._delegate.Codec().EncodeStart(ops, value);

            public override DataResult<A> Parse<U>(DynamicOps<U> ops, U input)
            {
                if (_type._index != _type._expectedIndex)
                {
                    return DataResult<A>.Error(() => "Index mismatch: " + _type._index + " != " + _type._expectedIndex);
                }
                return _type._delegate.Codec().Parse(ops, input);
            }
        }

        //fix returns nop when the element rewrite result is nop; otherwise projects via adapter and castOuter to a Check layer
        public static RewriteResult<A, object> Fix<A2>(CheckType<A2> type, RewriteResult<A2, object> instance)
        {
            if (instance.View().IsNop())
            {
                return (RewriteResult<A, object>)(object)RewriteResult<A2, object>.Nop(type);
            }
            //vanilla calls wrapOptic to castOuter the adapter's outer type to the new CheckType, guaranteeing newType stays a CheckType
            //BuildMuType takes the same-family path only when template==Check, otherwise the new family has size=0 and throws
            var adapter = TypedOptics.Adapter<A2, object>(instance.View().Type()!, instance.View().NewType()!)!;
            var wrappedOptic = WrapOptic(type, adapter);
            //instance is RewriteResult<A2,object>; casting to RewriteResult<object,object> fails when A2 is not object
            //OpticView returns RewriteResult<A2,Pair<string,object>> and similar; casting to RewriteResult<A,object> fails
            //both places use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var instanceObj = (object)instance;
            var instanceCasted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref instanceObj);
            var opticViewResult = T.Type<A2>.OpticView(type, instanceCasted,
                (TypedOptic<A2, object, object, object>)(object)wrappedOptic!);
            var opticViewObj = (object)opticViewResult;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<A, object>>(ref opticViewObj);
        }

        //wrapOptic casts the adapter's outer type to the new CheckType, aligning with vanilla CheckType.wrapOptic
        //the new CheckType's delegate is adapter.tType, i.e. instance.view().newType()
        private static TypedOptic<TA, object, TA, object> WrapOptic<TA>(CheckType<TA> type, TypedOptic<TA, object, TA, object> optic)
        {
            var newCheckType = new CheckType<object>(type._name, type._index, type._expectedIndex, optic.TType()!);
            var casted = optic.CastOuter(type!, newCheckType);
            return (TypedOptic<TA, object, TA, object>)(object)casted;
        }

        //all decides whether to verify the index match based on checkIndex, then delegates
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
        {
            if (checkIndex && _index != _expectedIndex)
            {
                return RewriteResult<A, object>.Nop(this);
            }
            return Fix(this, _delegate.RewriteOrNop(rule));
        }

        //one applies the rule to the delegate, then wraps with fix
        public override Optional<RewriteResult<A, object>> One(object rule)
        {
            var view = ((TypeRewriteRule)rule).Rewrite(_delegate);
            if (!view.IsPresent) return Optional<RewriteResult<A, object>>.Empty();
            return Optional<RewriteResult<A, object>>.Of(Fix(this, (RewriteResult<A, object>)view.Get()));
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)new CheckType<object>(_name, _index, _expectedIndex, _delegate.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.Check(_name, _expectedIndex, _delegate.Template());

        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => _index == _expectedIndex
                ? _delegate.FindFieldTypeOpt(name)
                : Optional<T.Type<object>>.Empty();

        //findCheckedType verifies the index matches then returns itself, maps to vanilla CheckType.findCheckedType
        //Schema.GetType calls FindCheckedType on a recursive point to get the CheckType-wrapped type
        public override Optional<T.Type<object>> FindCheckedType(int index)
            => _index == _expectedIndex
                ? Optional<T.Type<object>>.Of((T.Type<object>)(object)this)
                : _delegate.FindCheckedType(index);

        //findChoiceType delegates directly to the delegate, maps to vanilla CheckType.findChoiceType
        //Schema.FindChoiceType delegates from a CheckType to the inner TaggedChoiceType
        public override Optional<object> FindChoiceType(string name, int index)
            => _delegate.FindChoiceType(name, index);

        //findTypeInChildren delegates to delegate.findType and wrapOptic wraps the outer type as a CheckType, aligning with vanilla CheckType.findTypeInChildren
        //NamedChoiceFinder.Match only hits a TaggedChoiceType; on a CheckType it returns Continue, then FindTypeInChildren is used
        //vanilla delegates to the delegate here so Match retries and hits on the TaggedChoiceType
        public override Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeInChildren<FT, FR>(
            T.Type<FT> type, T.Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        {
            if (_index != _expectedIndex)
            {
                return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>
                    .Right(new FieldNotFoundException("Incorrect index in CheckType"));
            }
            var delegateResult = _delegate.FindType(type, resultType, matcher, recurse);
            if (delegateResult.IsRight) return delegateResult;
            var optic = delegateResult.GetLeft().Get();
            //optic is TypedOptic<object,object,FT,FR>; castOuter swaps the outer object for CheckType<A>
            //builds a new CheckType<object> using optic.TType() as the delegate to preserve the type chain
            var newCheckType = new CheckType<object>(_name, _index, _expectedIndex, optic.TType()!);
            var casted = optic.CastOuterUncheckedObject((object)newCheckType!, (object)newCheckType!);
            return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Left(casted);
        }

        public override Optional<A> Point<T>(DynamicOps<T> ops)
            => _index == _expectedIndex
                ? _delegate.Point(ops)
                : Optional<A>.Empty();

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (o is not CheckType<A> type)
            {
                return false;
            }
            if (_index == type._index && _expectedIndex == type._expectedIndex)
            {
                if (!checkIndex) return true;
                var delEqual = _delegate.Equals(type._delegate, ignoreRecursionPoints, checkIndex);
                if (delEqual) return true;
            }
            return false;
        }

        public override int GetHashCode()
            => unchecked(_index * 31 * 31 + _expectedIndex * 31 + (_delegate?.GetHashCode() ?? 0));
    }
}
