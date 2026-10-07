namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//List list template maps to vanilla com.mojang.datafixers.types.templates.List
//wraps the element type as List<A>
public sealed record List(TypeTemplate Element) : TypeTemplate
{
    public int Size() => Element.Size();

    //apply returns DSL.list-wrapped types at each index
    public TypeFamily Apply(TypeFamily family)
        => new ListFamily(this, family);

    //applyO uses the element template's applyO at each index, then wraps with cap as a list traversal
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)CapOptic(Element.ApplyO(input, aType, bType).Apply(i)));

    //CapOptic wraps the element optic with ListTraversal as a list traversal and composes it
    //vanilla capOptic takes AType()/BType() for aType/bType, not SType()/TType()
    //DSL.List and Compose casts both use Unsafe.As to bypass C# strict generic invariance, aligning with Java type erasure
    private static TypedOptic<object, object, A, B> CapOptic<S, T2, A, B>(TypedOptic<S, T2, A, B> concreteOptic)
    {
        var sListObj = (object)DSL.List(concreteOptic.SType())!;
        var sListType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref sListObj);
        var tListObj = (object)DSL.List(concreteOptic.TType())!;
        var tListType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref tListObj);
        var aTypeObj = (object)concreteOptic.AType();
        var aType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<A>>(ref aTypeObj);
        var bTypeObj = (object)concreteOptic.BType();
        var bType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<B>>(ref bTypeObj);
        var composeObj = (object)concreteOptic;
        var composeCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<A, B, A, B>>(ref composeObj);
        return new TypedOptic<object, object, A, B>(
            TypeClassesMarker.TraversalPToken,
            sListType,
            tListType,
            aType,
            bType,
            Optics.Optics.ListTraversal<A, B>()!).Compose(composeCast);
    }

    //findFieldOrType delegates to the element lookup, then wraps with List
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        var either = Element.FindFieldOrType(index, name, type, resultType);
        if (either.IsLeft)
        {
            return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Left(new List(either.GetLeft().Get()));
        }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Right(either.GetRight().Get());
    }

    //hmap applies hmap to the element at each index, then wraps with cap
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => i =>
        {
            var view = Element.Hmap(family, function)(i);
            return CapView(Apply(family).Apply(i), view);
        };

    //CapView wraps the element rewrite result into a list rewrite via ListType.fix
    private static RewriteResult<object, object> CapView<E>(T.Type<object> type, RewriteResult<E, object> view)
    {
        var fixResult = ((ListType<E>)(object)type).Fix<object>(view);
        //Fix returns RewriteResult<List<E>,object>; casting to RewriteResult<object,object> fails when List<E> is not object
        //use Unsafe.As to bypass the runtime type check and align with Java type erasure
        var fixObj = (object)fixResult;
        return System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref fixObj);
    }

    public override string ToString() => "List[" + Element + "]";

    //ListFamily returns the child type wrapped with DSL.list at each index
    private sealed class ListFamily : TypeFamily
    {
        private readonly List _template;
        private readonly TypeFamily _family;
        public ListFamily(List template, TypeFamily family)
        {
            _template = template;
            _family = family;
        }
        public T.Type<object> Apply(int index)
        {
            //element.Apply returns T.Type<A>; casting to T.Type<object> after wrapping as List<A> fails
            //both places use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
            var elementObj = (object)_template.Element.Apply(_family).Apply(index)!;
            var elementType = System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref elementObj);
            var listType = DSL.List(elementType);
            var listObj = (object)listType;
            return System.Runtime.CompilerServices.Unsafe.As<object, T.Type<object>>(ref listObj);
        }
    }

    //ListType list type holding the element type
    public sealed class ListType<A> : T.Type<List<A>>
    {
        private readonly T.Type<A> _element;

        public ListType(T.Type<A> element)
        {
            _element = element;
        }

        public T.Type<A> GetElement() => _element;

        public override RewriteResult<List<A>, object> All(object rule, bool recurse, bool checkIndex)
        {
            var view = _element.RewriteOrNop(rule);
            return Fix(view);
        }

        public override Optional<RewriteResult<List<A>, object>> One(object rule)
        {
            var rewritten = ((TypeRewriteRule)rule).Rewrite(_element);
            if (!rewritten.IsPresent) return Optional<RewriteResult<List<A>, object>>.Empty();
            return Optional<RewriteResult<List<A>, object>>.Of(Fix((RewriteResult<A, object>)rewritten.Get()));
        }

        public override T.Type<object> UpdateMu(RecursiveTypeFamily newFamily)
            => (T.Type<object>)(object)DSL.List(_element.UpdateMu(newFamily));

        public override TypeTemplate BuildTemplate()
            => DSL.List(_element.Template());

        public override Optional<List<A>> Point<T>(DynamicOps<T> ops)
            => Optional<List<A>>.Of(new List<A>());

        //fix projects the element rewrite result into the list layer via ListTraversal
        //TypedOptics.List returns TypedOptic<List<A>,List<B>,A,B>, inconsistent with the List<B>->object expected by OpticView
        //use Unsafe.As to bypass C# strict generic invariance, aligning with Java type erasure semantics
        public RewriteResult<List<A>, object> Fix<B>(RewriteResult<A, B> view)
        {
            var viewObj = (object)view;
            var viewCast = System.Runtime.CompilerServices.Unsafe.As<object, RewriteResult<object, object>>(ref viewObj);
            var opticObj = (object)TypedOptics.List<A, B>(_element, view.View().NewType())!;
            var opticCast = System.Runtime.CompilerServices.Unsafe.As<object, TypedOptic<List<A>, object, object, object>>(ref opticObj);
            return T.Type<List<A>>.OpticView(this, viewCast, opticCast);
        }

        protected override Codec<List<A>> BuildCodec()
            => new ListCodecAdapter(_element.Codec());

        //ListCodecAdapter list codec, maps to vanilla Codec.list
        //processes items one by one through the element codec's Parse/EncodeStart
        private sealed class ListCodecAdapter : ScalarCodec<List<A>>
        {
            private readonly Codec<A> _elementCodec;
            public ListCodecAdapter(Codec<A> elementCodec) { _elementCodec = elementCodec; }

            public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, List<A> value)
            {
                var stream = value.Select(t => _elementCodec.EncodeStart(ops, t).GetOrThrow());
                return DataResult<U>.Success(ops.CreateList(stream));
            }

            public override DataResult<List<A>> Parse<U>(DynamicOps<U> ops, U input)
                => ops.GetStream(input).Map(stream =>
                    (List<A>)stream.Select(t => _elementCodec.Parse(ops, t).GetOrThrow()).ToList());
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
            => o is ListType<A> other && _element.Equals(other._element, ignoreRecursionPoints, checkIndex);

        public override int GetHashCode() => _element?.GetHashCode() ?? 0;
    }
}
