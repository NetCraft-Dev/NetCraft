namespace NetCraft.DataFixer.Types.Templates;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Kinds;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//Const constant template maps to vanilla com.mojang.datafixers.types.templates.Const
//every index returns the same type; size is 0
public sealed record Const(T.Type<object> Type) : TypeTemplate
{
    public int Size() => 0;

    //apply ignores the family; every index returns the same type
    public TypeFamily Apply(TypeFamily family)
        => new ConstFamily(Type);

    //applyO returns id when the type matches aType, otherwise builds an ignore optic
    public FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
    {
        if (Equals(Type, (T.Type<object>)(object)aType!))
        {
            return TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)MakeIdOptic(aType, bType));
        }
        var ignoreOptic = MakeIgnoreOptic(Type, aType, bType);
        return TypeFamily.FamilyOptic<object, object>(i => (TypedOptic<object, object, object, object>)(object)ignoreOptic);
    }

    //MakeIdOptic uses Profunctor.id as the identity optic when the type matches
    private static TypedOptic<object, object, A, B> MakeIdOptic<A, B>(T.Type<A> aType, T.Type<B> bType)
        => new TypedOptic<object, object, A, B>(
            new HashSet<object> { TypeClassesMarker.ProfunctorToken },
            (T.Type<object>)(object)aType!,
            (T.Type<object>)(object)bType!,
            aType,
            bType,
            Optics.Optics.Id<object, object>()!);

    //MakeIgnoreOptic builds an ignore-focus optic with Affine when the type does not match, returning the original value
    private static TypedOptic<TT, TT, A, B> MakeIgnoreOptic<TT, A, B>(T.Type<TT> type, T.Type<A> aType, T.Type<B> bType)
        => new TypedOptic<TT, TT, A, B>(
            TypeClassesMarker.AffinePToken,
            type,
            type,
            aType,
            bType,
            Optics.Optics.Affine<TT, TT, A, B>(t => Either<TT, A>.Left(t), (b, t) => t));

    //findFieldOrType delegates to DSL.fieldFinder to search within Const.type
    public Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(
        int index, string? name, T.Type<A> type, T.Type<B> resultType)
    {
        var finder = DSL.FieldFinder<A>(name, type);
        var either = finder.FindType(Type, resultType, false);
        if (either.IsLeft)
            {
                return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>
                    .Left(new Const((T.Type<object>)(object)((TypedOptic<object, object, object, object>)(object)either.GetLeft().Get()).TType()!));
            }
        return Either<TypeTemplate, T.Type<object>.FieldNotFoundException>.Right(either.GetRight().Get());
    }

    //hmap ignores function and returns nop, aligning with vanilla Const.hmap
    //Const is a leaf with no recursive substructure; vanilla directly returns i->RewriteResult.nop(type)
    public Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => _ => RewriteResult<object, object>.Nop(Type);

    public override string ToString() => "Const[" + Type + "]";

    //ConstFamily fixed type family; every index returns the same type
    private sealed class ConstFamily : TypeFamily
    {
        private readonly T.Type<object> _type;
        public ConstFamily(T.Type<object> type) => _type = type;
        public T.Type<object> Apply(int index) => _type;
    }

    //PrimitiveType primitive type; encodes/decodes directly with a Codec without template expansion
    public sealed class PrimitiveType<A> : T.Type<A>
    {
        private readonly Codec<A> _codec;

        public PrimitiveType(Codec<A> codec)
        {
            _codec = codec;
        }

        public override bool Equals(object? o, bool ignoreRecursionPoints, bool checkIndex)
            => ReferenceEquals(this, o);

        public override TypeTemplate BuildTemplate()
            => DSL.ConstType(this);

        protected override Codec<A> BuildCodec() => _codec;

        public override string ToString() => _codec?.ToString() ?? "PrimitiveType";
    }
}

//TypeClassesMarker centralizes optic proof tokens, avoiding Templates depending back on the Kinds subpackage
internal static class TypeClassesMarker
{
    //ProfunctorToken maps to vanilla Profunctor.Mu.TYPE_TOKEN, using typeof(IProfunctorMu) to reflect interface inheritance
    public static readonly object ProfunctorToken = typeof(IProfunctorMu);
    //AffinePToken maps to vanilla AffineP.Mu.TYPE_TOKEN, using typeof(IAffinePMu) to reflect interface inheritance
    public static readonly object AffinePToken = typeof(IAffinePMu);
    //CartesianToken maps to vanilla Cartesian.Mu.TYPE_TOKEN, using typeof(ICartesianMu) to reflect interface inheritance
    public static readonly object CartesianToken = typeof(ICartesianMu);
    //TraversalPToken maps to vanilla TraversalP.Mu.TYPE_TOKEN, using typeof(ITraversalPMu) to reflect interface inheritance
    public static readonly object TraversalPToken = typeof(ITraversalPMu);
}
