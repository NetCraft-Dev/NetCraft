using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Named entity fix parent class, maps to vanilla net.minecraft.util.datafix.fixes.NamedEntityFix
//Matches a specific entity by entityName and applies the fix method; subclasses implement the concrete fix logic
public abstract class NamedEntityFix : DataFix
{
    private readonly string _name;
    protected readonly string EntityName;
    protected readonly DSL.ITypeReference TypeRef;

    protected abstract Typed<object> Fix(Typed<object> entity);

    public NamedEntityFix(Schema outputSchema, bool changesType, string name, DSL.ITypeReference type, string entityName)
        : base(outputSchema, changesType)
    {
        _name = name;
        TypeRef = type;
        EntityName = entityName;
    }

    protected override TypeRewriteRule MakeRule()
    {
        var inputChoiceType = GetInputSchema().GetChoiceType(TypeRef, EntityName);
        var entityF = DSL.NamedChoice(EntityName, inputChoiceType);
        var outputChoiceType = GetOutputSchema().GetChoiceType(TypeRef, EntityName);
        var inputType = GetInputSchema().GetType(TypeRef);
        var outputType = GetOutputSchema().GetType(TypeRef);
        return FixTypeEverywhereTyped(_name, inputType!, outputType!,
            input => input.UpdateTyped(entityF, outputChoiceType, entity => Fix(entity)));
    }
}
