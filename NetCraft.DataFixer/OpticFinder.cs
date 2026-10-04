namespace NetCraft.DataFixer;

using NetCraft.DataFixer.Types;

//OpticFinder optic查找器对应原版com.mojang.datafixers.OpticFinder
//在容器类型中查找指定焦点类型
public interface OpticFinder<FT>
{
    //type返回此查找器关注的类型
    Type<FT> Type();

    //findType在容器类型中查找焦点optic
    NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse);

    //findType重载默认resultType与自身类型相同
    NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FT>, Type<object>.FieldNotFoundException> FindType(
        Type<object> containerType, bool recurse)
        => FindType(containerType, Type(), recurse);

    //inField在指定字段内嵌套查找对应原版OpticFinder.inField
    //先按name+type在containerType中查找字段optic再组合外层查找焦点
    OpticFinder<FT> InField<GT>(string? name, Type<GT> type)
        => new InFieldOpticFinder<FT, GT>(this, name, type);
}

//InFieldOpticFinder嵌套字段查找器对应原版OpticFinder.inField的匿名类
//cap先查外层secondOptic失败直传成功则用DSL.fieldFinder查字段后compose
internal sealed class InFieldOpticFinder<FT, GT> : OpticFinder<FT>
{
    private readonly OpticFinder<FT> _outer;
    private readonly string? _name;
    private readonly Type<GT> _type;

    public InFieldOpticFinder(OpticFinder<FT> outer, string? name, Type<GT> type)
    {
        _outer = outer;
        _name = name;
        _type = type;
    }

    public Type<FT> Type() => _outer.Type();

    public NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse)
    {
        var secondOptic = _outer.FindType((Type<object>)(object)_type!, resultType, recurse);
        if (secondOptic.IsRight)
        {
            return NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException>
                .Right(secondOptic.GetRight().Get());
        }
        var l1 = (TypedOptic<object, object, FT, FR>)(object)secondOptic.GetLeft().Get();
        var first = DSL.FieldFinder<GT>(_name, _type).FindType(containerType, l1.TType(), recurse);
        if (first.IsRight)
        {
            return NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException>
                .Right(first.GetRight().Get());
        }
        var l = (TypedOptic<object, object, GT, FR>)(object)first.GetLeft().Get();
        var composed = l.Compose<FT, FR>((TypedOptic<GT, FR, FT, FR>)(object)l1);
        return NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException>
            .Left(composed);
    }
}
