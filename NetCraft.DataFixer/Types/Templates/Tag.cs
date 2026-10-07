namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Tag field template maps to vanilla com.mojang.datafixers.types.templates.Tag
//attaches a field name to the element template for MapCodec.fieldOf
public sealed record Tag(string Name, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Element.Size();

    //apply wraps the field type with DSL.field at each index
    public TypeFamily Apply(TypeFamily family)
        => new TagFamily(this, family);

    //applyO directly reuses the element's applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType matches the element by name then looks it up
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
        //returns its own template when the types are equal
        if (Equals(type, resultType))
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Left(this);
        }
        //on a recursive point match, returns itself or a constant template based on the index
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

    //hmap directly reuses the element's hmap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => Element.Hmap(family, function);

    public override string ToString() => "NameTag[" + Name + ": " + Element + "]";

    //TagFamily returns the child type wrapped with DSL.field at each index
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
            //element.Apply returns T.Type<A>; casting to T.Type<object> after wrapping as TagType<A> fails
            //both places use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var elementObj = (object)_template.Element.Apply(_family).Apply(index)!;
            var elementType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref elementObj);
            var tagType = DSL.Field(_template.Name, elementType);
            var tagObj = (object)tagType;
            return System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref tagObj);
        }
    }

    //TagType field type composed of a name and an element type
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

        //all applies the rule to the element, then wraps it back into the Tag layer
        public override RewriteResult<A, object> All(object rule, bool recurse, bool checkIndex)
            => Wrap(_element.RewriteOrNop(rule));

        //wrap projects the element rewrite result into the Tag layer via Profunctor.id
        //RewriteResult/TypedOptic casts both use Unsafe.As to bypass C# strict generic invariance, aligning with Java type erasure
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

        //one applies the rule to the element, then wraps with wrap
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

        //BuildFieldCodec wraps the element codec with fieldOf then takes codec, maps to vanilla element.codec().fieldOf(name).codec()
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

    //FieldCodecWrapper wraps a MapCodec as a Codec, maps to vanilla MapCodec.codec
    //loops to a MapLike via EncodeStart/Decode, then uses fieldOf codec
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
