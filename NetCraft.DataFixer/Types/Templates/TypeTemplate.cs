namespace NetCraft.DataFixer.Types.Templates;

using System;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Util;

//TypeTemplate类型模板对应原版com.mojang.datafixers.types.templates.TypeTemplate
//递归类型家族的模板描述如何构建子类型
public interface TypeTemplate
{
    //size模板的递归参数数量
    int Size();

    //apply应用到类型家族返回子类型
    TypeFamily Apply(TypeFamily family);

    //toSimpleType用空家族简化为简单类型
    T.Type<object> ToSimpleType()
        => Apply(new SimpleTypeFamily()).Apply(-1);

    //findFieldOrType查找字段或返回模板与未找到异常
    Either<TypeTemplate, T.Type<object>.FieldNotFoundException> FindFieldOrType<A, B>(int index, string? name, T.Type<A> type, T.Type<B> resultType)
        => throw new NotSupportedException("TypeTemplate.FindFieldOrType must be overridden");

    //hmap按家族+函数构造每个index的重写结果
    Func<int, RewriteResult<object, object>> Hmap(TypeFamily family, Func<int, RewriteResult<object, object>> function)
        => throw new NotSupportedException("TypeTemplate.Hmap must be overridden");

    //applyO应用到FamilyOptic产生新FamilyOptic
    FamilyOptic<object, object> ApplyO<A, B>(FamilyOptic<A, B> input, T.Type<A> aType, T.Type<B> bType)
        => throw new NotSupportedException("TypeTemplate.ApplyO must be overridden");
}

//SimpleTypeFamily空家族实现toSimpleType用返回空类型
internal sealed class SimpleTypeFamily : TypeFamily
{
    public T.Type<object> Apply(int index) => DslImpl.EmptyPartType();
}

//DslImpl占位类引用DSL.emptyPartType避免TypeTemplate直接依赖DSL根入口形成强循环
internal static class DslImpl
{
    public static T.Type<object> EmptyPartType() => (T.Type<object>)(object)DSL.EmptyPartType();
}
