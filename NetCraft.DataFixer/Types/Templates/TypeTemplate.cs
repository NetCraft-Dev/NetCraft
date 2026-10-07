namespace NetCraft.DataFixer.Types.Templates;

using System;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//TypeTemplate type template maps to vanilla com.mojang.datafixers.types.templates.TypeTemplate
//the template of a recursive type family, describing how to build child types
public interface TypeTemplate
{
    //size: the number of recursive parameters of the template
    int Size();

    //apply applies to a type family and returns the child type
    TypeFamily Apply(TypeFamily family);

    //toSimpleType simplifies to a simple type using an empty family
    T.Type<object> ToSimpleType()
        => Apply(new SimpleTypeFamily()).Apply(-1);

    //findFieldOrType looks up a field, or returns the template along with a not-found exception
    Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => throw new NotSupportedException("TypeTemplate.FindFieldOrType must be overridden");

    //hmap builds the rewrite result for each index from a family + function
    Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => throw new NotSupportedException("TypeTemplate.Hmap must be overridden");

    //applyO applies to a FamilyOptic to produce a new FamilyOptic
    FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => throw new NotSupportedException("TypeTemplate.ApplyO must be overridden");
}

//SimpleTypeFamily empty family implementation; toSimpleType returns the empty type
internal sealed class SimpleTypeFamily : TypeFamily
{
    public T.Type<object> Apply(int index) => DslImpl.EmptyPartType();
}

//DslImpl placeholder class referencing DSL.emptyPartType, avoiding a strong cycle from TypeTemplate directly depending on the DSL root entry
internal static class DslImpl
{
    public static T.Type<object> EmptyPartType() => (T.Type<object>)(object)DSL.EmptyPartType();
}
