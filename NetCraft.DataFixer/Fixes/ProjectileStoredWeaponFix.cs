using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//投射物存储武器修复对应原版ProjectileStoredWeaponFix
//1.21.4为arrow/spectral_arrow实体走write+fix+read流程应用identity修复类型转换
public class ProjectileStoredWeaponFix : DataFix
{
    public ProjectileStoredWeaponFix(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
    {
        var inputEntityType = GetInputSchema().GetType(References.Entity);
        var outputEntityType = GetOutputSchema().GetType(References.Entity);
        return FixTypeEverywhereTyped("Fix Arrow stored weapon", inputEntityType, outputEntityType,
            ExtraDataFixUtils.ChainAllFilters(FixChoice("minecraft:arrow"), FixChoice("minecraft:spectral_arrow")));
    }

    //fixChoice按实体名构造命名选择查找走write+read类型转换
    private Func<Typed<object>, Typed<object>> FixChoice(string entityName)
    {
        var inputEntityChoiceType = GetInputSchema().GetChoiceType(References.Entity, entityName);
        var outputEntityChoiceType = GetOutputSchema().GetChoiceType(References.Entity, entityName);
        var entityF = DSL.NamedChoice(entityName, inputEntityChoiceType);
        return input => input.UpdateTyped(entityF, outputEntityChoiceType,
            typed => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputEntityChoiceType, d => d));
    }
}
