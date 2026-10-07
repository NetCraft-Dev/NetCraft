namespace NetCraft.DataFixer.Types.Templates;

using System;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using NetCraft.DataFixer.Optics;
using NetCraft.DataFixer.Optics.Profunctors;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;
using OpticsClass = NetCraft.DataFixer.Optics.Optics;

//Named named wrapper template maps to vanilla com.mojang.datafixers.types.templates.Named
//attaches a name to the element type for debugging and codecs
public sealed record Named(string Name, TypeTemplate Element) : TypeTemplate
{
    public int Size() => Element.Size();

    //apply wraps with DSL.named at each index
    public TypeFamily Apply(TypeFamily family)
        => new NamedFamily(this, family);

    //applyO directly reuses the element's applyO
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => Element.ApplyO(input, aType, bType).Apply(i));

    //findFieldOrType delegates directly to the element
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => Element.FindFieldOrType(index, name, type, resultType);

    //hmap applies hmap to the element at each index, then wraps it as a Named via cap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => index =>
        {
            var elementResult = Element.Hmap(family, function)(index);
            return Cap(family, index, elementResult);
        };

    //Cap wraps the element rewrite result into a Named layer via NamedType.fix
    private RewriteResult<object, object> Cap<A>(TypeFamily family, int index, RewriteResult<A, object> elementResult)
    {
        var typeObj = Apply(family).Apply(index)!;
        //NamedFamily.Apply wraps NamedType with TypeObjectWrapper, aligning with Java type erasure
        //Cap needs a NamedType<A> instance, taken from Inner
        var namedType = typeObj is T.TypeObjectWrapper w
            ? (NamedType<A>)w.Inner
            : (NamedType<A>)(object)typeObj;
        var fixResult = NamedType<A>.Fix(namedType, elementResult);
        //NamedType.Fix returns RewriteResult<Pair<string,A>,object>; casting to RewriteResult<object,object> fails
        //use Unsafe.As to bypass the runtime type check and align with Java type erasure
        var fixObj = (object)fixResult;
        return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref fixObj);
    }

    public override string ToString() => "NamedTypeTag[" + Name + ": " + Element + "]";

    //NamedFamily returns the child type wrapped with DSL.named at each index
    private sealed class NamedFamily : TypeFamily
    {
        private readonly Named _template;
        private readonly TypeFamily _family;
        public NamedFamily(Named template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
            => T.TypeObjectConverterFactory.AsObjectType(
                DSL.Named(_template.Name,
                    _template.Element.Apply(_family).Apply(index)!));
    }

    //NamedType named type holding Pair<String,A> as its value
    public sealed class NamedType<A> : T.Type<NetCraft.DataFixer.Util.Pair<string, A>>
    {
        private readonly string _name;
        private readonly T.Type<A> _element;

        public NamedType(string name, T.Type<A> element)
        {
            _name = name;
            _element = element;
        }

        public string Name() => _name;
        public T.Type<A> Element() => _element;

        //fix returns nop when the element rewrite result is nop; otherwise projects via Proj2+Adapter, aligning with vanilla NamedType.fix+wrapOptic
        //vanilla wrapOptic uses Optics.proj2() to extract Pair.Second at the outer level, then composes the inner optic
        public static RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object> Fix<A2>(
            NamedType<A2> type, RewriteResult<A2, object> instance)
        {
            if (instance.View().IsNop())
            {
                return (RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>)(object)RewriteResult<NetCraft.DataFixer.Util.Pair<string, A2>, object>.Nop(type);
            }
            var newType = T.TypeObjectConverterFactory.AsObjectType(
                DSL.Named(type.Name(), instance.View().NewType()!))!;
            //newType is a TypeObjectWrapper inheriting Type<object>; it cannot be cast directly to Type<Pair<string,object>>
            //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var newTypeObj = (object)newType!;
            var newTypeCast = System.Runtime.CompilerServices.Unsafe.As<object, Type<NetCraft.DataFixer.Util.Pair<string, object>>>(ref newTypeObj);
            //the outer Proj2 extracts Pair.Second, aligning with vanilla Optics.proj2()
            var proj2Optic = new TypedOptic<NetCraft.DataFixer.Util.Pair<string, A2>, NetCraft.DataFixer.Util.Pair<string, object>, A2, object>(
                typeof(ICartesianMu),
                type!,
                newTypeCast,
                instance.View().Type()!,
                instance.View().NewType()!,
                OpticsClass.Proj2<string, A2, object>());
            //the inner Adapter goes from A2 to object, aligning with vanilla TypedOptic.adapter(view.type, view.newType)
            var innerAdapter = TypedOptics.Adapter<A2, object>(
                instance.View().Type()!,
                instance.View().NewType()!);
            //the outer Proj2 composes the inner Adapter, aligning with vanilla .compose(optic)
            var wrappedOptic = proj2Optic.Compose(innerAdapter);
            var wrappedObj = (object)wrappedOptic;
            var wrappedCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<NetCraft.DataFixer.Util.Pair<string, A2>, NetCraft.DataFixer.Util.Pair<string, object>, object, object>>(ref wrappedObj);
            var opticViewResult = T.Type<NetCraft.DataFixer.Util.Pair<string, A2>>.OpticView(type!,
                (RewriteResult<object, object>)(object)instance,
                wrappedCast);
            //OpticView returns RewriteResult<Pair<string,A2>,Pair<string,object>>; casting to RewriteResult<Pair<string,A>,object> fails
            //use Unsafe.As to bypass the T generic invariance and align with Java type erasure
            var opticViewObj = (object)opticViewResult;
            return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>>(ref opticViewObj);
        }

        public override RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object> All(object rule, bool recurse, bool checkIndex)
        {
            var elementView = _element.RewriteOrNop(rule);
            return Fix(this, elementView);
        }

        public override Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>> One(object rule)
        {
            var view = ((TypeRewriteRule)rule).Rewrite(_element);
            if (!view.IsPresent) return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>>.Empty();
            return Optional<RewriteResult<NetCraft.DataFixer.Util.Pair<string, A>, object>>.Of(Fix(this, (RewriteResult<A, object>)view.Get()));
        }

        //findFieldTypeOpt delegates to the named element
        public override Optional<T.Type<object>> FindFieldTypeOpt(string name)
            => _element.FindFieldTypeOpt(name);

        //findChoiceType delegates to the named element, maps to vanilla NamedType.findChoiceType
        public override Optional<object> FindChoiceType(string name, int index)
            => _element.FindChoiceType(name, index);

        //findTypeInChildren delegates to element.findType and wrapOptic wraps the outer type as a NamedType, aligning with vanilla NamedType.findTypeInChildren
        //NamedChoiceFinder.Match misses on a NamedType and returns Continue, then FindTypeInChildren delegates to element so Match retries and hits on the TaggedChoiceType
        //wrapOptic uses Optics.proj2 to project NamedType onto element, then composes optic
        public override Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException> FindTypeInChildren<FT, FR>(
            T.Type<FT> type, T.Type<FR> resultType, TypeMatcher<FT, FR> matcher, bool recurse)
        {
            //NamedType<A> inherits Type<Pair<string,A>>; _element is Type<A>
            //TypeMatcher/FieldNotFoundException are nested types of Type<A> and differ at compile time across Type<X> instantiations
            //use Unsafe.As to convert the outer matcher and Either to the corresponding types of Type<A>
            var elemMatcher = System.Runtime.CompilerServices.Unsafe.As<TypeMatcher<FT, FR>, T.Type<A>.TypeMatcher<FT, FR>>(ref matcher!);
            var elemResultObj = (object)_element.FindType(type, resultType, elemMatcher, recurse);
            var elementResult = System.Runtime.CompilerServices.Unsafe.As<object, Either<TypedOptic<object, object, FT, FR>, T.Type<A>.FieldNotFoundException>>(ref elemResultObj);
            if (elementResult.IsRight)
            {
                //Continue/FieldNotFoundException are nested types of Type<X> and are different CLR types across Type instantiations
                //cross-generic is checks fail, so detect Continue semantics by type name, aligning with vanilla cross-Type delegation behavior
                var rightObj = (object)elementResult.GetRight().Get();
                if (rightObj.GetType().Name == "Continue")
                {
                    return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(new Continue());
                }
                return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Right(new FieldNotFoundException(rightObj.ToString()!));
            }
            var optic = elementResult.GetLeft().Get();
            //the outer proj2 projects Pair<String,A> to A, then composes optic applying A->B
            //sType/tType are wrapped with DSL.named to preserve the NamedType outer type chain
            var namedSType = (T.Type<NetCraft.DataFixer.Util.Pair<string, object>>)(object)DSL.Named(_name, optic.SType()!)!;
            var namedTType = (T.Type<NetCraft.DataFixer.Util.Pair<string, object>>)(object)DSL.Named(_name, optic.TType()!)!;
            var proj2Optic = new TypedOptic<NetCraft.DataFixer.Util.Pair<string, object>, NetCraft.DataFixer.Util.Pair<string, object>, object, object>(
                typeof(ICartesianMu),
                namedSType!,
                namedTType!,
                optic.SType()!,
                optic.TType()!,
                Optics.Proj2<string, object, object>());
            //proj2Optic's inner focus is Pair<string,object>=optic.S/T; Compose swaps the focus to optic.A/B
            //aligns with the vanilla wrap method's wrapOptic.compose(optic)
            var composed = proj2Optic.Compose(optic);
            //the composed outer Pair<string,object> needs castOuter to object/object to match the return type
            var casted = composed.CastOuterUncheckedObject((object)namedSType!, (object)namedTType!);
            var composedObj = (object)casted;
            var composedCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<object, object, FT, FR>>(ref composedObj);
            return Either<TypedOptic<object, object, FT, FR>, FieldNotFoundException>.Left(composedCast);
        }

        //findCheckedType delegates to the named element, maps to vanilla NamedType.findCheckedType
        public override Optional<T.Type<object>> FindCheckedType(int index)
            => _element.FindCheckedType(index);

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.Named(_name, _element.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.Named(_name, _element.Template());

        //buildCodec aligns with vanilla: decode attaches name, encode verifies name then delegates to the element codec
        protected override Codec<NetCraft.DataFixer.Util.Pair<string, A>> BuildCodec()
            => new NamedCodec(this);

        //NamedCodec named codec; decode attaches name to the value, encode verifies the name matches then delegates to the element
        private sealed class NamedCodec : ScalarCodec<NetCraft.DataFixer.Util.Pair<string, A>>
        {
            private readonly NamedType<A> _type;
            public NamedCodec(NamedType<A> type) => _type = type;

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, NetCraft.DataFixer.Util.Pair<string, A> input)
            {
                if (!Equals(input.First, _type._name))
                {
                    return DataResult<U>.Error(() => "Named type name doesn't match: expected: " + _type._name + ", got: " + input.First);
                }
                //_element is Type<A> at compile time but may be TaggedChoiceType<string> at runtime, inheriting Type<Pair<string,object>>
                //the virtual method Type<A>.Codec() is absent from the runtime type's vtable, throwing EntryPointNotFoundException
                //wrap with AsObjectType as a TypeObjectWrapper and invoke reflectively, aligning with Java type erasure
                var wrapped = T.TypeObjectConverterFactory.AsObjectType(_type._element);
                return wrapped.Codec().EncodeStart(ops, input.Second);
            }

            public override DataResult<NetCraft.DataFixer.Util.Pair<string, A>> Parse<U>(DynamicOps<U> ops, U input)
            {
                var wrapped = T.TypeObjectConverterFactory.AsObjectType(_type._element);
                var result = wrapped.Codec().Parse(ops, input);
                //result is DataResult<object> but the actual value is type A; use Unsafe.As to cast, aligning with Java type erasure
                var casted = System.Runtime.CompilerServices.Unsafe.As<DataResult<object>, DataResult<A>>(ref result);
                return casted.Map(v => NetCraft.DataFixer.Util.Pair<string, A>.Of(_type._name, v));
            }
        }

        public override Optional<NetCraft.DataFixer.Util.Pair<string, A>> Point<T>(DynamicOps<T> ops)
        {
            var elementPoint = _element.Point(ops);
            if (!elementPoint.IsPresent) return Optional<NetCraft.DataFixer.Util.Pair<string, A>>.Empty();
            return Optional<NetCraft.DataFixer.Util.Pair<string, A>>.Of(NetCraft.DataFixer.Util.Pair<string, A>.Of(_name, elementPoint.Get()));
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
        {
            if (ReferenceEquals(this, o)) return true;
            if (o is not NamedType<A> other)
            {
                return false;
            }
            var nameEqual = Equals(_name, other._name);
            var elemEqual = _element.Equals(other._element, ignoreRecursionPoints, checkIndex);
            return nameEqual && elemEqual;
        }

        public override int GetHashCode()
            => unchecked((_name?.GetHashCode() ?? 0) * 31 + (_element?.GetHashCode() ?? 0));
    }
}
