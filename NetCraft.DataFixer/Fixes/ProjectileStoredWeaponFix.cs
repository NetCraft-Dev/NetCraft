using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//projectile stored weapon fix, maps to vanilla ProjectileStoredWeaponFix
//1.21.4 runs arrow/spectral_arrow entities through the write+fix+read flow, applying identity to fix the type conversion
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

    //fixChoice builds a named choice finder by entity name and goes through the write+read type conversion
    private Func<Typed<object>, Typed<object>> FixChoice(string entityName)
    {
        var inputEntityChoiceType = GetInputSchema().GetChoiceType(References.Entity, entityName);
        var outputEntityChoiceType = GetOutputSchema().GetChoiceType(References.Entity, entityName);
        var entityF = DSL.NamedChoice(entityName, inputEntityChoiceType);
        return input => input.UpdateTyped(entityF, outputEntityChoiceType,
            typed => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputEntityChoiceType, d => d));
    }
}
