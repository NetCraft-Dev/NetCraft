namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;
using NetCraft.Util;

//RecursivePoint recursive point template maps to vanilla com.mojang.datafixers.types.templates.RecursivePoint
//references the recursive type at an index in the family
public sealed record RecursivePoint(int Index) : TypeTemplate
{
    public int Size() => Index + 1;

    //apply caches family.Apply(Index) as the return for all indexes
    public TypeFamily Apply(TypeFamily family)
    {
        var result = family.Apply(Index);
        return new RecursivePointFamily(result);
    }

    //applyO always returns the optic at the family's Index
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)input.Apply(Index));

    //findFieldOrType: a recursive point cannot look up fields
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
            .Right(new T.Type<object>.FieldNotFoundException("Recursion point"));

    //hmap calls cap with the rewrite result of the element at Index
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => i =>
        {
            var result = function(Index);
            return Cap(family, result);
        };

    //Cap verifies that sourceType matches the result view type, then clones the BitSet and sets the index
    public RewriteResult<S, T2> Cap<S, T2>(TypeFamily family, RewriteResult<S, T2> result)
    {
        var sourceType = family.Apply(Index);
        if (sourceType is not RecursivePointType<object>)
        {
            throw new ArgumentException("Type error: Recursive point template got a non-recursive type as input.");
        }
        if (!Equals(result.View().Type(), sourceType))
        {
            throw new ArgumentException("Type error: hmap function input type");
        }
        var recData = result.RecData();
        //vanilla clones the BitSet then sets the index; here we support BitSet or fall back to the original value
        var bitSet = recData as BitSet;
        if (bitSet != null)
        {
            var cloned = bitSet.Clone();
            cloned.Set(Index);
            return RewriteResult<S, T2>.Create(result.View(), cloned);
        }
        return RewriteResult<S, T2>.Create(result.View(), recData);
    }

    public override string ToString() => "Id[" + Index + "]";

    //RecursivePointFamily always returns result, ignoring the index
    private sealed class RecursivePointFamily : TypeFamily
    {
        private readonly T.Type<object> _result;
        public RecursivePointFamily(T.Type<object> result) => _result = result;
        public T.Type<object> Apply(int index) => _result;
    }

    //RecursivePointType recursive point type; lazily unfolds the type at the corresponding index in the family
    public sealed class RecursivePointType<A> : T.Type<A>
    {
        private readonly RecursiveTypeFamily _family;
        private readonly int _index;
        private readonly Func<T.Type<A>> _delegate;
        private volatile T.Type<A>? _type;

        public RecursivePointType(RecursiveTypeFamily family, int index, Func<T.Type<A>> @delegate)
        {
            _family = family;
            _index = index;
            _delegate = @delegate;
        }

        public RecursiveTypeFamily Family() => _family;
        public int Index() => _index;

        //unfold lazily unfolds the corresponding type in the family
        public T.Type<A> Unfold()
        {
            if (_type == null) _type = _delegate();
            return _type;
        }

        //buildCodec lazily delegates to the unfolded type's codec
        protected override Codec<A> BuildCodec()
            => new RecursivePointCodec(this);

        //RecursivePointCodec recursive point codec; lazily delegates to unfold.codec
        private sealed class RecursivePointCodec : ScalarCodec<A>
        {
            private readonly RecursivePointType<A> _type;
            public RecursivePointCodec(RecursivePointType<A> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, A value)
            {
                //unfold may return a non-Type<A> instance wrapped by Unsafe.As (e.g. SumType<object,object> when A=object)
                //calling Codec directly returns a codec whose runtime type does not match, so interface dispatch fails with EntryPointNotFoundException
                //wrap with AsObjectType so Codec returns a CodecAdapter that truly implements Codec<A>
                var unfolded = _type.Unfold();
                var wrapped = TypeObjectConverterFactory.AsObjectType(unfolded);
                var codec = wrapped.Codec();
                return codec.EncodeStart(ops, value);
            }

            public override DataResult<A> Parse<U>(DynamicOps<U> ops, U input)
            {
                var unfolded = _type.Unfold();
                var wrapped = TypeObjectConverterFactory.AsObjectType(unfolded);
                var codec = wrapped.Codec();
                var result = codec.Parse(ops, input);
                return (DataResult<A>)(object)result;
            }
        }

        //all delegates directly to unfold.all
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
            => Unfold().All(rule, recurse, checkIndex);

        //one delegates directly to unfold.one
        public override Optional<RewriteResult<A, object>> One(object rule)
            => Unfold().One(rule);

        //findFieldTypeOpt delegates the lookup to unfold
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => Unfold().FindFieldTypeOpt(name);

        //findCheckedType delegates to unfold's lookup of the check-wrapped TaggedChoiceType
        //maps to vanilla RecursivePointType.findCheckedType
        public override Optional<T.Type<object>> FindCheckedType(int index)
            => Unfold().FindCheckedType(index);

        //everywhere delegates to the family's everywhere when recursing, otherwise nop
        public override Optional<RewriteResult<A, object>> Everywhere(object rule, object optimizationRule, bool recurse, bool checkIndex)
        {
            if (recurse)
            {
                var everywhereOpt = _family.Everywhere(_index, rule, (PointFreeRule)optimizationRule);
                if (everywhereOpt.IsPresent)
                {
                    return Optional<RewriteResult<A, object>>.Of((RewriteResult<A, object>)(object)everywhereOpt.Get());
                }
            }
            return Optional<RewriteResult<A, object>>.Of(RewriteResult<A, object>.Nop(this));
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => newFamily.Apply(_index);

        public override TypeTemplate BuildTemplate()
            => DSL.Id(_index);

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (o is not RecursivePointType<A> type) return false;
            return (ignoreRecursionPoints || Equals(_family, type._family)) && _index == type._index;
        }

        public override int GetHashCode()
            => unchecked((_family?.GetHashCode() ?? 0) * 31 + _index);

        //in builds a View that folds into itself
        public View<A, A> In() => View<A, A>.Create(Functions.In(this), this, this);
        //out builds a View that expands to unfold
        public View<A, A> Out() => View<A, A>.Create(Functions.Out(this), this, this);
    }
}
