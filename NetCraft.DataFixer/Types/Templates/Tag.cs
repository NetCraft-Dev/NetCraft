namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Tag字段模板对应原版com.mojang.datafixers.types.templates.Tag
//给元素模板附加字段名用于MapCodec.fieldOf
public sealed record Tag(string Name, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Element.Size();

    //apply每个index用DSL.field包装字段类型
    public TypeFamily Apply(TypeFamily family)
        => new TagFamily(this, family);

    //applyO直接复用元素的applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType按名字匹配元素再查找
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        if (!Equals(name, Name))
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                .Right(new T.Type<object>.FieldNotFoundException("Names don't match"));
        }
        if (Element is Const c)
        {
            if (Equals(type, (T.Type<A>)(object)c.Type!))
            {
                return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                    .Left(new Tag(Name, new Const((T.Type<object>)(object)resultType!)));
            }
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                .Right(new T.Type<object>.FieldNotFoundException("don't match"));
        }
        //类型相同时返回自身模板
        if (Equals(type, resultType))
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Left(this);
        }
        //递归点匹配时按索引判断后返回自身或常量模板
        if (type is RecursivePoint.RecursivePointType<A> rpType && Element is RecursivePoint rp)
        {
            if (rp.Index == rpType.Index())
            {
                if (resultType is RecursivePoint.RecursivePointType<B> rpResult)
                {
                    if (rpResult.Index() == rp.Index)
                    {
                        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Left(this);
                    }
                }
                else
                {
                    return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                        .Left(DSL.ConstType((T.Type<object>)(object)resultType!));
                }
            }
        }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
            .Right(new T.Type<object>.FieldNotFoundException("Recursive field"));
    }

    //hmap直接复用元素的hmap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => Element.Hmap(family, function);

    public override string ToString() => "NameTag[" + Name + ": " + Element + "]";

    //TagFamily按index返回DSL.field包装的子类型
    private sealed class TagFamily : TypeFamily
    {
        private readonly Tag _template;
        private readonly TypeFamily _family;
        public TagFamily(Tag template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
        {
            //element.Apply返回T.Type<A>包装为TagType<A>后强转T.Type<object>会失败
            //两处都用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
            var elementObj = (object)_template.Element.Apply(_family).Apply(index)!;
            var elementType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref elementObj);
            var tagType = DSL.Field(_template.Name, elementType);
            var tagObj = (object)tagType;
            return System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref tagObj);
        }
    }

    //TagType字段类型由名称与元素类型组成
    public sealed class TagType<A> : T.Type<A>
    {
        private readonly string _name;
        private readonly T.Type<A> _element;

        public TagType(string name, T.Type<A> element)
        {
            _name = name;
            _element = element;
        }

        public string Name() => _name;
        public T.Type<A> Element() => _element;

        //all对元素应用规则后用wrap包装回Tag层
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
            => Wrap(_element.RewriteOrNop(rule));

        //wrap元素重写结果用Profunctor.id投射到Tag层
        //RewriteResult/TypedOptic强转都用Unsafe.As绕过C#严格泛型不变量对齐Java类型擦除
        private RewriteResult<A, object> Wrap<B>(RewriteResult<A, B> instance)
        {
            if (instance.View().IsNop())
            {
                var nopObj = (object)instance;
                return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<A, object>>(ref nopObj);
            }
            var outputObj = (object)DSL.Field(_name, instance.View().NewType()!)!;
            var output = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<A>>(ref outputObj);
            var viewObj = (object)instance;
            var viewCast = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            var opticObj = (object)new TypedOptic<A, A, A, B>(
                TypeClassesMarker.ProfunctorToken,
                this,
                output,
                _element,
                instance.View().NewType()!,
                Optics.Optics.Id<A, B>()!);
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<A, A, object, object>>(ref opticObj);
            var resultObj = (object)T.Type<A>.OpticView(this, viewCast, opticCast);
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<A, object>>(ref resultObj);
        }

        //one对元素应用规则后用wrap包装
        public override Optional<RewriteResult<A, object>> One(object rule)
        {
            var view = ((TypeRewriteRule)rule).Rewrite(_element);
            if (!view.IsPresent) return Optional<RewriteResult<A, object>>.Empty();
            return Optional<RewriteResult<A, object>>.Of(Wrap((RewriteResult<A, object>)view.Get()));
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.Field(_name, _element.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.Field(_name, _element.Template());

        protected override Codec<A> BuildCodec()
            => BuildFieldCodec(_element.Codec(), _name);

        //BuildFieldCodec元素codec用fieldOf包装后取codec对应原版element.codec().fieldOf(name).codec()
        private static Codec<A> BuildFieldCodec(Codec<A> elementCodec, string name)
            => (Codec<A>)(object)new FieldCodecWrapper<A>(elementCodec, name);

        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => Equals(name, _name)
                ? Optional<T.Type<object>>.Of((T.Type<object>)(object)_element)
                : Optional<T.Type<object>>.Empty();

        public override Optional<A> Point<T>(DynamicOps<T> ops)
            => _element.Point(ops);

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (ReferenceEquals(this, o)) return true;
            if (o is not TagType<A> tagType) return false;
            return Equals(_name, tagType._name) && _element.Equals(tagType._element, ignoreRecursionPoints, checkIndex);
        }

        public override int GetHashCode()
            => unchecked((_name?.GetHashCode() ?? 0) * 31 + (_element?.GetHashCode() ?? 0));
    }

    //FieldCodecWrapper包装MapCodec为Codec对应原版MapCodec.codec
    //通过EncodeStart/Decode循环到MapLike再用fieldOf codec
    private sealed class FieldCodecWrapper<A> : ScalarCodec<A>
    {
        private readonly Codec<A> _elementCodec;
        private readonly string _name;
        public FieldCodecWrapper(Codec<A> elementCodec, string name)
        {
            _elementCodec = elementCodec;
            _name = name;
        }

        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, A value)
        {
            var fieldCodec = _elementCodec.FieldOf(_name);
            var builder = ops.MapBuilder();
            fieldCodec.EncodeTo(ops, value, builder);
            return builder.Build(ops.Empty());
        }

        public override DataResult<A> Parse<U>(DynamicOps<U> ops, U input)
        {
            var fieldCodec = _elementCodec.FieldOf(_name);
            return ops.GetMap(input).FlatMap(map => fieldCodec.Decode(ops, map));
        }
    }
}
