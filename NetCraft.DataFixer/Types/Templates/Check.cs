namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Check检查模板对应原版com.mojang.datafixers.types.templates.Check
//按index匹配时才使用元素模板用于版本检查
public sealed record Check(string Name, int Index, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Math.Max(Index + 1, Element.Size());

    //apply每个index构造CheckType包装元素类型
    public TypeFamily Apply(TypeFamily family)
        => new CheckFamily(this, family);

    //applyO直接复用元素的applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType仅在index匹配时委托给元素查找
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

    //hmap每个index对元素应用hmap后用cap包装为Check
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var elementResult = Element.Hmap(family, function)(index);
            return Cap(family, index, elementResult);
        };

    //Cap把元素重写结果用CheckType.fix包装为Check层
    //A可能是Pair<string,object>等复杂类型强转RewriteResult<object,object>失败
    //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
    private RewriteResult<object, object> Cap<A>(TypeFamily family, int index, RewriteResult<A, object> elementResult)
    {
        var fixResult = CheckType<A>.Fix((CheckType<A>)(object)Apply(family).Apply(index)!, elementResult);
        var fixObj = (object)fixResult;
        return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref fixObj);
    }

    public override string ToString() => "Tag[" + Name + ", " + Index + ": " + Element + "]";

    //CheckFamily按index返回CheckType包装的子类型
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

    //CheckType带index检查的包装类型
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

        //buildCodec按原版用delegate.codec加index校验decode
        protected override Codec<A> BuildCodec()
            => new CheckCodec(this);

        //CheckCodec检查codec decode校验index匹配后委托delegate
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

        //fix元素重写结果为nop时返回nop否则用adapter投射并castOuter为Check层
        public static RewriteResult<A, object> Fix<A2>(CheckType<A2> type, RewriteResult<A2, object> instance)
        {
            if (instance.View().IsNop())
            {
                return (RewriteResult<A, object>)(object)RewriteResult<A2, object>.Nop(type);
            }
            //原版调wrapOptic把adapter外层castOuter为新CheckType保证newType仍是CheckType
            //BuildMuType依赖template==Check才走同家族路径否则新家族size=0抛异常
            var adapter = TypedOptics.Adapter<A2, object>(instance.View().Type()!, instance.View().NewType()!)!;
            var wrappedOptic = WrapOptic(type, adapter);
            //instance是RewriteResult<A2,object>强转RewriteResult<object,object>在A2非object时失败
            //OpticView返回RewriteResult<A2,Pair<string,object>>等强转RewriteResult<A,object>失败
            //两处都用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var instanceObj = (object)instance;
            var instanceCasted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref instanceObj);
            var opticViewResult = T.Type<A2>.OpticView(type, instanceCasted,
                (TypedOptic<A2, object, object, object>)(object)wrappedOptic!);
            var opticViewObj = (object)opticViewResult;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<A, object>>(ref opticViewObj);
        }

        //wrapOptic把adapter外层castOuter为新CheckType对齐原版CheckType.wrapOptic
        //新CheckType的delegate是adapter.tType即instance.view().newType()
        private static TypedOptic<TA, object, TA, object> WrapOptic<TA>(CheckType<TA> type, TypedOptic<TA, object, TA, object> optic)
        {
            var newCheckType = new CheckType<object>(type._name, type._index, type._expectedIndex, optic.TType()!);
            var casted = optic.CastOuter(type!, newCheckType);
            return (TypedOptic<TA, object, TA, object>)(object)casted;
        }

        //all按checkIndex决定是否校验index匹配后委托delegate
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
        {
            if (checkIndex && _index != _expectedIndex)
            {
                return RewriteResult<A, object>.Nop(this);
            }
            return Fix(this, _delegate.RewriteOrNop(rule));
        }

        //one对delegate应用规则后用fix包装
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

        //findCheckedType检查index匹配后返回自身对应原版CheckType.findCheckedType
        //Schema.GetType在递归点上调用FindCheckedType获取CheckType包装的类型
        public override Optional<T.Type<object>> FindCheckedType(int index)
            => _index == _expectedIndex
                ? Optional<T.Type<object>>.Of((T.Type<object>)(object)this)
                : _delegate.FindCheckedType(index);

        //findChoiceType直接委托delegate对应原版CheckType.findChoiceType
        //Schema.FindChoiceType在CheckType上委托到内层TaggedChoiceType
        public override Optional<object> FindChoiceType(string name, int index)
            => _delegate.FindChoiceType(name, index);

        //findTypeInChildren委托delegate.findType并wrapOptic包装外层为CheckType对齐原版CheckType.findTypeInChildren
        //NamedChoiceFinder.Match只命中TaggedChoiceType在CheckType上返回Continue后走FindTypeInChildren
        //原版在这里委托到delegate让Match在TaggedChoiceType上重试命中
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
            //optic是TypedOptic<object,object,FT,FR>需castOuter把外层object换成CheckType<A>
            //构造新CheckType<object>用optic.TType()作为delegate保持类型链
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
