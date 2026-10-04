using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//命名实体修复父类对应原版net.minecraft.util.datafix.fixes.NamedEntityFix
//按entityName匹配特定实体并应用fix方法子类实现具体修复逻辑
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
