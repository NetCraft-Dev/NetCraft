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

//RecursivePoint递归点模板对应原版com.mojang.datafixers.types.templates.RecursivePoint
//按index引用家族中的递归类型
public sealed record RecursivePoint(int Index) : TypeTemplate
{
    public int Size() => Index + 1;

    //apply缓存family.Apply(Index)作为所有index的返回
    public TypeFamily Apply(TypeFamily family)
    {
        var result = family.Apply(Index);
        return new RecursivePointFamily(result);
    }

    //applyO固定返回家族Index位置的optic
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)input.Apply(Index));

    //findFieldOrType递归点不可查找字段
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
            .Right(new T.Type<object>.FieldNotFoundException("Recursion point"));

    //hmap用Index位置元素的重写结果调用cap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => i =>
        {
            var result = function(Index);
            return Cap(family, result);
        };

    //Cap校验sourceType类型与结果view类型匹配后克隆BitSet并设置index
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
        //原版用BitSet.clone后set index这里支持BitSet或回退原值
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

    //RecursivePointFamily固定返回result忽略index
    private sealed class RecursivePointFamily : TypeFamily
    {
        private readonly T.Type<object> _result;
        public RecursivePointFamily(T.Type<object> result) => _result = result;
        public T.Type<object> Apply(int index) => _result;
    }

    //RecursivePointType递归点类型延迟展开家族中对应index的类型
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

        //unfold惰性展开家族中对应类型
        public T.Type<A> Unfold()
        {
            if (_type == null) _type = _delegate();
            return _type;
        }

        //buildCodec惰性委托给unfold的codec
        protected override Codec<A> BuildCodec()
            => new RecursivePointCodec(this);

        //RecursivePointCodec递归点codec惰性委托unfold.codec
        private sealed class RecursivePointCodec : ScalarCodec<A>
        {
            private readonly RecursivePointType<A> _type;
            public RecursivePointCodec(RecursivePointType<A> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, A value)
            {
                //unfold可能返回Unsafe.As包装的非Type<A>实例（如SumType<object,object>当A=object）
                //直接调Codec返回的codec运行时类型不匹配接口分派失败抛EntryPointNotFoundException
                //用AsObjectType包装后Codec返回CodecAdapter真正实现Codec<A>
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

        //all直接委托给unfold.all
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
            => Unfold().All(rule, recurse, checkIndex);

        //one直接委托给unfold.one
        public override Optional<RewriteResult<A, object>> One(object rule)
            => Unfold().One(rule);

        //findFieldTypeOpt委托给unfold的查找
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => Unfold().FindFieldTypeOpt(name);

        //findCheckedType委托给unfold查找check包装的TaggedChoiceType
        //对应原版RecursivePointType.findCheckedType
        public override Optional<T.Type<object>> FindCheckedType(int index)
            => Unfold().FindCheckedType(index);

        //everywhere递归时委托家族everywhere否则nop
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

        //in构造折叠到自身的View
        public View<A, A> In() => View<A, A>.Create(Functions.In(this), this, this);
        //out构造展开到unfold的View
        public View<A, A> Out() => View<A, A>.Create(Functions.Out(this), this, this);
    }
}
