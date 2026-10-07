namespace NetCraft.DataFixer;

using NetCraft.DataFixer.Types;

//OpticFinder optic finder maps to vanilla com.mojang.datafixers.OpticFinder
//finds a given focus type within a container type
public interface OpticFinder<FT>
{
    //type returns the type this finder focuses on
    Type<FT> Type();

    //findType finds the focus optic within the container type
    NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FR>, Type<object>.FieldNotFoundException> FindType<FR>(
        Type<object> containerType, Type<FR> resultType, bool recurse);

    //findType overload; resultType defaults to the same as its own type
    NetCraft.DataFixer.Util.Either<TypedOptic<object, object, FT, FT>, Type<object>.FieldNotFoundException> FindType(
        Type<object> containerType, bool recurse)
        => FindType(containerType, Type(), recurse);

    //inField nests a lookup within the given field, maps to vanilla OpticFinder.inField
    //first finds the field optic in containerType by name+type, then composes the outer focus lookup
    OpticFinder<FT> InField<GT>(string? name, Type<GT> type)
        => new InFieldOpticFinder<FT, GT>(this, name, type);
}

//InFieldOpticFinder nested field finder, maps to the anonymous class in vanilla OpticFinder.inField
//cap first looks up the outer secondOptic; on failure it passes through, on success it looks up the field via DSL.fieldFinder and composes
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
