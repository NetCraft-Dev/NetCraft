using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//parent for named entity fixes, maps to vanilla net.minecraft.util.datafix.fixes.NamedEntityFix
//matches a specific entity by entityName and applies fix; subclasses implement the concrete fix logic
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
        return FixTypeEverywhereTyped(_name, GetInputSchema().GetType(TypeRef), GetOutputSchema().GetType(TypeRef),
            input =>
            {
                var updated = input.UpdateTyped(entityF, outputChoiceType, entity => Fix(entity));
                return updated;
            });
    }
}
