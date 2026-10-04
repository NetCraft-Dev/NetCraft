namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;
using Functions = NetCraft.DataFixer.Functions.Functions;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//TaggedChoice带标签选择模板对应原版TaggedChoice
//按key分派到不同子类型用于实体/方块实体类型选择
public sealed class TaggedChoice<K> : TypeTemplate
{
    private readonly string _name;
    private readonly T.Type<K> _keyType;
    private readonly Dictionary<K, TypeTemplate> _templates;
    private readonly int _size;

    public TaggedChoice(string name, T.Type<K> keyType, Dictionary<K, TypeTemplate> templates)
    {
        _name = name;
        _keyType = keyType;
        _templates = templates;
        _size = templates.Values.Select(t => t.Size()).DefaultIfEmpty(0).Max();
    }

    public int Size() => _size;
    public string Name() => _name;
    public T.Type<K> KeyType() => _keyType;
    public Dictionary<K, TypeTemplate> Templates() => _templates;

    //apply返回按index取模板的TypeFamily带缓存
    public TypeFamily Apply(TypeFamily family)
        => new TaggedChoiceFamily<K>(this, family);

    //applyO原版不支持抛UnsupportedOperationException这里返回空FamilyOptic
    public FamilyOptic<A, B> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => throw new NotSupportedException("TaggedChoice.applyO not supported");

    //findFieldOrType原版未实现返回FieldNotFoundException
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
            .Right(new T.Type<object>.FieldNotFoundException("Not implemented"));

    //hmap按index对每个模板应用hmap后用elementResult逐个compose
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var initialType = (TaggedChoiceType<K>)(object)Apply(family).Apply(index)!;
            var result = RewriteResult<NetCraft.DataFixer.Util.Pair<K, object>, object>.Nop(initialType);
            foreach (var entry in _templates)
            {
                var elementResult = entry.Value.Hmap(family, function)(index);
                var currentType = (TaggedChoiceType<K>)(object)result.View().NewType()!;
                var elementRewrite = TaggedChoiceType<K>.ElementResult(entry.Key, currentType, elementResult);
                //elementRewrite和result是RewriteResult<Pair<K,object>,object>严格不变量下强转RewriteResult<object,object>失败
                //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
                var elementRewriteObj = (object)elementRewrite;
                var elementRewriteCasted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref elementRewriteObj);
                var resultObj = (object)result;
                var resultCasted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref resultObj);
                var composed = elementRewriteCasted.Compose(resultCasted);
                var composedObj = (object)composed;
                result = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<NetCraft.DataFixer.Util.Pair<K, object>, object>>(ref composedObj);
            }
            var finalObj = (object)result;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref finalObj);
        };

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not TaggedChoice<K> other) return false;
        return Equals(_name, other._name) && Equals(_keyType, other._keyType) && Equals(_templates, other._templates);
    }

    public override int GetHashCode()
        => unchecked((_name?.GetHashCode() ?? 0) * 31 * 31 + (_keyType?.GetHashCode() ?? 0) * 31 + (_templates?.GetHashCode() ?? 0));

    public override string ToString()
        => "TaggedChoice[" + _name + ", " + string.Join(", ", _templates.Select(kv => kv.Key + " -> " + kv.Value)) + "]";

    //TaggedChoiceFamily按family+index缓存构造TaggedChoiceType
    private sealed class TaggedChoiceFamily<KK> : TypeFamily
    {
        private readonly TaggedChoice<KK> _template;
        private readonly TypeFamily _family;
        private readonly Dictionary<NetCraft.Codec.Pair<TypeFamily, int>, T.Type<object>> _cache = new();
        private readonly object _lock = new();
        public TaggedChoiceFamily(TaggedChoice<KK> template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
        {
            var key = NetCraft.Codec.Pair<TypeFamily, int>.Of(_family, index);
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var cached)) return cached;
                var types = new Dictionary<KK, T.Type<object>>();
                foreach (var entry in _template.Templates())
                {
                    types[entry.Key] = entry.Value.Apply(_family).Apply(index);
                }
                //TaggedChoiceType<K>继承T.Type<Pair<K,object>>强转T.Type<object>会失败
                //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
                var taggedChoiceType = DSL.TaggedChoiceType(_template.Name(), _template.KeyType(), types);
                var taggedObj = (object)taggedChoiceType;
                var result = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref taggedObj);
                _cache[key] = result;
                return result;
            }
        }
    }

    //TaggedChoiceType带标签选择的具体类型按key分派值
    public sealed class TaggedChoiceType<K2> : T.Type<NetCraft.DataFixer.Util.Pair<K2, object>>
    {
        private readonly string _name;
        private readonly T.Type<K2> _keyType;
        private readonly Dictionary<K2, T.Type<object>> _types;
        private readonly int _hashCode;

        public TaggedChoiceType(string name, T.Type<K2> keyType, Dictionary<K2, T.Type<object>> types)
        {
            _name = name;
            _keyType = keyType;
            _types = types;
            _hashCode = ((_name, _keyType, _types).GetHashCode());
        }

        public string GetName() => _name;
        public T.Type<K2> GetKeyType() => _keyType;
        public Dictionary<K2, T.Type<object>> Types() => _types;
        public bool HasType(K2 key) => _types.ContainsKey(key);

        //all对每个子类型应用规则非nop的结果合并到新TaggedChoiceType
        public override RewriteResult<NetCraft.DataFixer.Util.Pair<K2, object>, object> All(object rule, bool recurse, bool checkIndex)
        {
            var results = new Dictionary<K2, RewriteResult<object, object>>();
            foreach (var entry in _types)
            {
                var result = ((TypeRewriteRule)rule).Rewrite(entry.Value);
                if (result.IsPresent && !result.Get().View().IsNop())
                {
                    results[entry.Key] = result.Get();
                }
            }
            if (results.Count == 0)
            {
                return RewriteResult<NetCraft.DataFixer.Util.Pair<K2, object>, object>.Nop(this);
            }
            if (results.Count == 1)
            {
                var entry = results.First();
                return ElementResult(entry.Key, this, entry.Value);
            }
            var newTypes = new Dictionary<K2, T.Type<object>>(_types);
            foreach (var entry in results)
            {
                newTypes[entry.Key] = entry.Value.View().NewType()!;
            }
            //多结果用TaggedChoiceRewriteFunc构造新View
            var newType = (T.Type<NetCraft.DataFixer.Util.Pair<K2, object>>)(object)DSL.TaggedChoiceType(_name, _keyType, newTypes)!;
            var view = View<NetCraft.DataFixer.Util.Pair<K2, object>, NetCraft.DataFixer.Util.Pair<K2, object>>.Create(
                Functions.Fun("TaggedChoiceTypeRewriteResult " + results.Count,
                    ops => input =>
                    {
                        if (!results.TryGetValue(input.First, out var rr)) return input;
                        return CapRuleApply(ops, input, rr);
                    },
                    this, newType),
                this, newType);
            return RewriteResult<NetCraft.DataFixer.Util.Pair<K2, object>, object>.Create((View<NetCraft.DataFixer.Util.Pair<K2, object>, object>)(object)view, new NetCraft.Util.BitSet());
        }

        //CapRuleApply对input应用对应结果view的function
        private static NetCraft.DataFixer.Util.Pair<K2, object> CapRuleApply<A, B>(DynamicOps<object> ops, NetCraft.DataFixer.Util.Pair<K2, object> input, RewriteResult<A, B> result)
        {
            var function = result.View().Function!.EvalCached();
            return NetCraft.DataFixer.Util.Pair<K2, object>.Of(input.First, function(ops)((A)input.Second)!);
        }

        //elementResult按key把子类型重写结果用TypedOptic.tagged投射到TaggedChoice层
        //type强转为TaggedChoice<K3>.TaggedChoiceType<K3>对齐TypedOptics.Tagged签名
        public static RewriteResult<NetCraft.DataFixer.Util.Pair<K3, object>, object> ElementResult<K3, FT, FR>(
            K3 key, TaggedChoiceType<K3> type, RewriteResult<FT, FR> result)
        {
            //result强转RewriteResult<object,object>在FR非object时失败用Unsafe.As绕过
            var resultObj = (object)result;
            var resultCasted = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref resultObj);
            var opticViewResult = T.Type<NetCraft.DataFixer.Util.Pair<K3, object>>.OpticView(type,
                resultCasted,
                (TypedOptic<NetCraft.DataFixer.Util.Pair<K3, object>, NetCraft.DataFixer.Util.Pair<K3, object>, object, object>)(object)
                TypedOptics.Tagged<K3, FT, FR>((TaggedChoice<K3>.TaggedChoiceType<K3>)(object)type!, key, result.View().Type()!, result.View().NewType()!)!);
            //OpticView返回RewriteResult<Pair<K3,object>,Pair<K3,object>>强转RewriteResult<Pair<K3,object>,object>失败
            //用Unsafe.As绕过运行时类型检查对齐Java类型擦除
            var opticViewObj = (object)opticViewResult;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<NetCraft.DataFixer.Util.Pair<K3, object>, object>>(ref opticViewObj);
        }

        //one对子类型逐个应用规则首个命中即返回
        public override Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<K2, object>, object>> One(object rule)
        {
            foreach (var entry in _types)
            {
                var elementResult = ((TypeRewriteRule)rule).Rewrite(entry.Value);
                if (elementResult.IsPresent)
                {
                    return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<K2, object>, object>>.Of(
                        ElementResult(entry.Key, this, (RewriteResult<object, object>)elementResult.Get()));
                }
            }
            return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<K2, object>, object>>.Empty();
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
        {
            var newTypes = new Dictionary<K2, T.Type<object>>();
            foreach (var entry in _types)
            {
                newTypes[entry.Key] = entry.Value.UpdateMu(newFamily);
            }
            return (T.Type<object>)(object)DSL.TaggedChoiceType(_name, _keyType, newTypes)!;
        }

        public override TypeTemplate BuildTemplate()
        {
            var templates = new Dictionary<K2, TypeTemplate>();
            foreach (var entry in _types)
            {
                templates[entry.Key] = entry.Value.Template();
            }
            return (TypeTemplate)(object)DSL.TaggedChoice(_name, _keyType, templates);
        }

        //buildCodec用keyType.codec按partialDispatch分派到子类型codec
        protected override Codec<NetCraft.DataFixer.Util.Pair<K2, object>> BuildCodec()
            => new TaggedChoiceCodec(this);

        //TaggedChoiceCodec按key分派到对应子类型codec
        private sealed class TaggedChoiceCodec : ScalarCodec<NetCraft.DataFixer.Util.Pair<K2, object>>
        {
            private readonly TaggedChoiceType<K2> _type;
            public TaggedChoiceCodec(TaggedChoiceType<K2> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.DataFixer.Util.Pair<K2, object> value)
            {
                if (!_type._types.TryGetValue(value.First, out var elementType))
                {
                    return DataResult<U>.Error(() => "Unsupported key: " + value.First);
                }
                //先编码value得到entity map再把keyEncoded作为_type._name字段值添加
                //对齐原版partialDispatch把key字段合并到value map的语义
                var valueEncoded = ((Codec<object>)(object)elementType.Codec()!).EncodeStart(ops, value.Second).GetOrThrow();
                var keyEncoded = _type._keyType.Codec().EncodeStart(ops, value.First).GetOrThrow();
                return ops.MergeToMap(valueEncoded, ops.CreateString(_type._name), keyEncoded);
            }

            public override DataResult<NetCraft.DataFixer.Util.Pair<K2, object>> Parse<U>(DynamicOps<U> ops, U input)
            {
                var map = ops.GetMap(input).GetOrThrow();
                //取key字段值
                var keyOpt = map.Get(_type._name);
                if (!keyOpt.IsPresent)
                {
                    return DataResult<NetCraft.DataFixer.Util.Pair<K2, object>>.Error(() => "Missing key field: " + _type._name);
                }
                var keyResult = _type._keyType.Codec().Parse(ops, keyOpt.Get());
                if (!keyResult.Result().IsPresent)
                {
                    return DataResult<NetCraft.DataFixer.Util.Pair<K2, object>>.Error(() => "Failed to parse key");
                }
                var key = keyResult.GetOrThrow();
                if (!_type._types.TryGetValue(key, out var elementType))
                {
                    return DataResult<NetCraft.DataFixer.Util.Pair<K2, object>>.Error(() => "Unsupported key: " + key);
                }
                var valueResult = ((Codec<object>)(object)elementType.Codec()!).Parse(ops, input);
                if (!valueResult.Result().IsPresent)
                {
                    return DataResult<NetCraft.DataFixer.Util.Pair<K2, object>>.Error(() => "Failed to parse value for key: " + key);
                }
                return DataResult<NetCraft.DataFixer.Util.Pair<K2, object>>.Success(NetCraft.DataFixer.Util.Pair<K2, object>.Of(key, valueResult.GetOrThrow()));
            }
        }

        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
        {
            foreach (var t in _types.Values)
            {
                var opt = t.FindFieldTypeOpt(name);
                if (opt.IsPresent) return opt;
            }
            return Optional<T.Type<object>>.Empty();
        }

        public override Optional<NetCraft.DataFixer.Util.Pair<K2, object>> Point<T>(DynamicOps<T> ops)
        {
            foreach (var entry in _types)
            {
                var pointOpt = entry.Value.Point(ops);
                if (pointOpt.IsPresent)
                {
                    return Optional<NetCraft.DataFixer.Util.Pair<K2, object>>.Of(
                        NetCraft.DataFixer.Util.Pair<K2, object>.Of(entry.Key, pointOpt.Get()));
                }
            }
            return Optional<NetCraft.DataFixer.Util.Pair<K2, object>>.Empty();
        }

        //point按指定key与value构造Typed若key不存在返回空
        public Optional<Typed<NetCraft.DataFixer.Util.Pair<K2, object>>> Point<T>(DynamicOps<T> ops, K2 key, object value)
        {
            if (!_types.ContainsKey(key)) return Optional<Typed<NetCraft.DataFixer.Util.Pair<K2, object>>>.Empty();
            return Optional<Typed<NetCraft.DataFixer.Util.Pair<K2, object>>>.Of(
                new Typed<NetCraft.DataFixer.Util.Pair<K2, object>>(this, (DynamicOps<object>)(object)ops,
                    NetCraft.DataFixer.Util.Pair<K2, object>.Of(key, value)));
        }

        public override Optional<object> FindChoiceType(string name, int index)
            => Equals(name, _name)
                ? Optional<object>.Of(this)
                : Optional<object>.Empty();

        public override Optional<T.Type<object>> FindCheckedType(int index)
        {
            foreach (var type in _types.Values)
            {
                var opt = type.FindCheckedType(index);
                if (opt.IsPresent) return opt;
            }
            return Optional<T.Type<object>>.Empty();
        }

        public override bool Equals(object? obj, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not TaggedChoiceType<K2> other)
            {
                return false;
            }
            var nameEqual = Equals(_name, other._name);
            var keyEqual = _keyType.Equals(other._keyType, ignoreRecursionPoints, checkIndex);
            var countEqual = _types.Count == other._types.Count;
            if (!nameEqual) return false;
            if (!keyEqual) return false;
            if (!countEqual) return false;
            foreach (var entry in _types)
            {
                if (!other._types.TryGetValue(entry.Key, out var otherType))
                {
                    return false;
                }
                if (!entry.Value.Equals(otherType, ignoreRecursionPoints, checkIndex))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode() => _hashCode;
    }
}
