namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//CompoundList复合列表模板对应原版CompoundList
//键值对的列表List<Pair<K,V>>模板形式
public sealed record CompoundList(TypeTemplate Key, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Math.Max(Key.Size(), Element.Size());

    //apply每个index返回DSL.compoundList(key, element)包装
    public TypeFamily Apply(TypeFamily family)
        => new CompoundListFamily(this, family);

    //applyO用元素applyO的结果cap包装为复合列表遍历
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)CapOptic(Element.ApplyO(input, aType, bType).Apply(i)));

    //CapOptic把元素optic包装为复合列表元素遍历
    //方法类型参数T2避免与using别名T冲突CS0704
    private static TypedOptic<object, object, A, B> CapOptic<S, T2, A, B>(TypedOptic<S, T2, A, B> concreteOptic)
    {
        var sTypeEntry = (T.Type<NetCraft.DataFixer.Util.Pair<string, S>>)(object)DSL.And(DSL.String(), concreteOptic.SType())!;
        var tTypeEntry = (T.Type<NetCraft.DataFixer.Util.Pair<string, T2>>)(object)DSL.And(DSL.String(), concreteOptic.TType())!;
        return new TypedOptic<object, object, A, B>(
            TypeClassesMarker.TraversalPToken,
            (T.Type<object>)(object)DSL.CompoundList(concreteOptic.SType())!,
            (T.Type<object>)(object)DSL.CompoundList(concreteOptic.TType())!,
            (T.Type<A>)(object)sTypeEntry,
            (T.Type<B>)(object)tTypeEntry,
            Optics.Optics.ListTraversal<A, B>()!)
            .Compose((TypedOptic<A, B, A, B>)(object)new TypedOptic<NetCraft.DataFixer.Util.Pair<string, S>, NetCraft.DataFixer.Util.Pair<string, T2>, A, B>(
                TypeClassesMarker.CartesianToken,
                sTypeEntry,
                tTypeEntry,
                (T.Type<A>)(object)concreteOptic.SType(),
                (T.Type<B>)(object)concreteOptic.TType(),
                Optics.Optics.Proj2<string, S, T2>()!))
            .Compose((TypedOptic<A, B, A, B>)(object)concreteOptic);
    }

    //findFieldOrType委托给元素查找后用CompoundList包装
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        var either = Element.FindFieldOrType(index, name, type, resultType);
        if (either.IsLeft)
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Left(new CompoundList(Key, either.GetLeft().Get()));
        }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Right(either.GetRight().Get());
    }

    //hmap对两侧元素应用hmap后用cap合并
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => i =>
        {
            var f1 = Key.Hmap(family, function)(i);
            var f2 = Element.Hmap(family, function)(i);
            return CapView(Apply(family).Apply(i), f1, f2);
        };

    //CapView两侧元素重写结果用CompoundListType.mergeViews合并
    private static RewriteResult<object, object> CapView<L, R>(T.Type<object> type, RewriteResult<L, object> f1, RewriteResult<R, object> f2)
        => (RewriteResult<object, object>)(object)((CompoundListType<L, R>)(object)type).MergeViews(f1, f2);

    public override string ToString() => "CompoundList[" + Element + "]";

    //CompoundListFamily按index返回DSL.compoundList包装的子类型
    private sealed class CompoundListFamily : TypeFamily
    {
        private readonly CompoundList _template;
        private readonly TypeFamily _family;
        public CompoundListFamily(CompoundList template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
            => (T.Type<object>)(object)DSL.CompoundList(
                (T.Type<object>)(object)_template.Key.Apply(_family).Apply(index)!,
                (T.Type<object>)(object)_template.Element.Apply(_family).Apply(index)!);
    }

    //CompoundListType复合列表类型持有key与value类型
    public sealed class CompoundListType<K, V> : T.Type<List<NetCraft.DataFixer.Util.Pair<K, V>>>
    {
        private readonly T.Type<K> _key;
        private readonly T.Type<V> _element;

        public CompoundListType(T.Type<K> key, T.Type<V> element)
        {
            _key = key;
            _element = element;
        }

        public T.Type<K> GetKey() => _key;
        public T.Type<V> GetElement() => _element;

        //all对两侧元素应用规则后mergeViews合并
        public override RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object> All(object rule, bool recurse, bool checkIndex)
            => MergeViews(_key.RewriteOrNop(rule), _element.RewriteOrNop(rule));

        //mergeViews分两步先fixKeys再fixValues后compose
        //类型擦除后Compose类型不匹配用object强转对齐原版Java语义
        public RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object> MergeViews(
            RewriteResult<K, object> leftView, RewriteResult<V, object> rightView)
        {
            var v1 = FixKeys(this, _key, _element, leftView);
            var v2 = FixValues((T.Type<List<NetCraft.DataFixer.Util.Pair<K, V>>>)(object)v1.View().NewType()!, _key, _element, rightView);
            return (RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object>)(object)v2.Compose((RewriteResult<object, List<NetCraft.DataFixer.Util.Pair<K, V>>>)(object)v1);
        }

        //one先尝试key再尝试element任意命中即返回
        public override Optional<RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object>> One(object rule)
        {
            var keyOpt = ((TypeRewriteRule)rule).Rewrite(_key);
            if (keyOpt.IsPresent)
            {
                return Optional<RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object>>.Of(
                    FixKeys(this, _key, _element, (RewriteResult<K, object>)keyOpt.Get()));
            }
            var elementOpt = ((TypeRewriteRule)rule).Rewrite(_element);
            if (elementOpt.IsPresent)
            {
                return Optional<RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object>>.Of(
                    FixValues(this, _key, _element, (RewriteResult<V, object>)elementOpt.Get()));
            }
            return Optional<RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object>>.Empty();
        }

        //fixKeys把key侧重写结果用compoundListKeys投射到列表层
        private static RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object> FixKeys(
            T.Type<List<NetCraft.DataFixer.Util.Pair<K, V>>> type, T.Type<K> first, T.Type<V> second, RewriteResult<K, object> view)
            => T.Type<List<NetCraft.DataFixer.Util.Pair<K, V>>>.OpticView(type, (RewriteResult<object, object>)(object)view,
                (TypedOptic<List<NetCraft.DataFixer.Util.Pair<K, V>>, object, object, object>)(object)TypedOptics.CompoundListKeys<K, V, object>(first, (T.Type<object>)(object)view.View().NewType()!, second)!);

        //fixValues把value侧重写结果用compoundListElements投射到列表层
        private static RewriteResult<List<NetCraft.DataFixer.Util.Pair<K, V>>, object> FixValues(
            T.Type<List<NetCraft.DataFixer.Util.Pair<K, V>>> type, T.Type<K> first, T.Type<V> second, RewriteResult<V, object> view)
            => T.Type<List<NetCraft.DataFixer.Util.Pair<K, V>>>.OpticView(type, (RewriteResult<object, object>)(object)view,
                (TypedOptic<List<NetCraft.DataFixer.Util.Pair<K, V>>, object, object, object>)(object)TypedOptics.CompoundListElements<K, V, object>(first, second, (T.Type<object>)(object)view.View().NewType()!)!);

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.CompoundList(_key.UpdateMu(newFamily), _element.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => new CompoundList(_key.Template(), _element.Template());

        public override Optional<List<NetCraft.DataFixer.Util.Pair<K, V>>> Point<T>(DynamicOps<T> ops)
            => Optional<List<NetCraft.DataFixer.Util.Pair<K, V>>>.Of(new List<NetCraft.DataFixer.Util.Pair<K, V>>());

        protected override Codec<List<NetCraft.DataFixer.Util.Pair<K, V>>> BuildCodec()
            => new CompoundListCodec(this);

        //CompoundListCodec复合列表codec用List<Pair<K,V>>结构对应原版Codec.compoundList
        private sealed class CompoundListCodec : ScalarCodec<List<NetCraft.DataFixer.Util.Pair<K, V>>>
        {
            private readonly CompoundListType<K, V> _type;
            public CompoundListCodec(CompoundListType<K, V> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, List<NetCraft.DataFixer.Util.Pair<K, V>> value)
            {
                var stream = value.Select(p =>
                {
                    var keyEncoded = _type._key.Codec().EncodeStart(ops, p.First);
                    var valueEncoded = _type._element.Codec().EncodeStart(ops, p.Second);
                    //合并key/value为map后整体作为list元素
                    return ops.MergeToMap(ops.EmptyMap(), keyEncoded.GetOrThrow(), valueEncoded.GetOrThrow()).GetOrThrow();
                });
                return DataResult<U>.Success(ops.CreateList(stream));
            }

            public override DataResult<List<NetCraft.DataFixer.Util.Pair<K, V>>> Parse<U>(DynamicOps<U> ops, U input)
            {
                return ops.GetStream(input).Map(stream =>
                    (List<NetCraft.DataFixer.Util.Pair<K, V>>)stream.Select(t =>
                    {
                        var map = ops.GetMap(t).GetOrThrow();
                        //map中第一个entry作为key/value这里简化原版dispatch逻辑
                        var entries = ops.GetMapValues(t).GetOrThrow().ToList();
                        if (entries.Count == 0)
                        {
                            return NetCraft.DataFixer.Util.Pair<K, V>.Of(_type._key.Codec().Parse(ops, ops.Empty()).GetOrThrow(),
                                _type._element.Codec().Parse(ops, ops.Empty()).GetOrThrow());
                        }
                        var entry = entries[0];
                        var key = _type._key.Codec().Parse(ops, entry.First).GetOrThrow();
                        var val = _type._element.Codec().Parse(ops, entry.Second).GetOrThrow();
                        return NetCraft.DataFixer.Util.Pair<K, V>.Of(key, val);
                    }).ToList());
            }
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (o is not CompoundListType<K, V> that) return false;
            return _key.Equals(that._key, ignoreRecursionPoints, checkIndex)
                && _element.Equals(that._element, ignoreRecursionPoints, checkIndex);
        }

        public override int GetHashCode()
            => unchecked((_key?.GetHashCode() ?? 0) * 31 + (_element?.GetHashCode() ?? 0));
    }
}
