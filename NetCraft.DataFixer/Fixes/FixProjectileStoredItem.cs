using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//projectile stored item fix, maps to vanilla FixProjectileStoredItem
//1.20.5 adds an Item field for trident/arrow/spectral_arrow entities; trident converts directly, arrow/spectral_arrow branch by potion type
public class FixProjectileStoredItem : DataFix
{
    private const string EMPTY_POTION = "minecraft:empty";

    public FixProjectileStoredItem(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
    {
        var inputEntityType = GetInputSchema().GetType(References.Entity);
        var outputEntityType = GetOutputSchema().GetType(References.Entity);
        return FixTypeEverywhereTyped("Fix AbstractArrow item type", inputEntityType, outputEntityType,
            ExtraDataFixUtils.ChainAllFilters(
                FixChoice("minecraft:trident", CastUnchecked),
                FixChoice("minecraft:arrow", FixArrow),
                FixChoice("minecraft:spectral_arrow", FixSpectralArrow)));
    }

    //subFixer delegate signature: takes an input Typed and output Type, returns a new Typed
    private delegate Typed<object> SubFixer(Typed<object> input, T.Type<object> outputType);

    //fixChoice builds a named choice finder by entity name and applies the fix function
    private Func<Typed<object>, Typed<object>> FixChoice(string entityName, SubFixer fixer)
    {
        var inputEntityChoiceType = GetInputSchema().GetChoiceType(References.Entity, entityName);
        var outputEntityChoiceType = GetOutputSchema().GetChoiceType(References.Entity, entityName);
        var entityF = DSL.NamedChoice(entityName, inputEntityChoiceType);
        return input => input.UpdateTyped(entityF, outputEntityChoiceType, typed => fixer(typed, outputEntityChoiceType));
    }

    //fixArrow checks the Potion field to decide arrow vs tipped_arrow and writes the Item field
    private static Typed<object> FixArrow(Typed<object> typed, T.Type<object> outputType)
        => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputType,
            input => input.Set(FixConstants.DecoratedPotBlockEntityItem, CreateItemStack(input, GetArrowType(input))));

    //getArrowType decides arrow vs tipped_arrow based on whether the Potion field is empty
    private static string GetArrowType(Dynamic<object> input)
        => input.Get("Potion").AsString(EMPTY_POTION).Equals(EMPTY_POTION) ? "minecraft:arrow" : "minecraft:tipped_arrow";

    //fixSpectralArrow writes the spectral_arrow Item field
    private static Typed<object> FixSpectralArrow(Typed<object> typed, T.Type<object> outputType)
        => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputType,
            input => input.Set(FixConstants.DecoratedPotBlockEntityItem, CreateItemStack(input, "minecraft:spectral_arrow")));

    //createItemStack builds the {id:name,Count:1} item map
    private static Dynamic<object> CreateItemStack(Dynamic<object> input, string itemName)
        => input.CreateMap(new[]
        {
            new Pair<Dynamic<object>, Dynamic<object>>(input.CreateString("id"), input.CreateString(itemName)),
            new Pair<Dynamic<object>, Dynamic<object>>(input.CreateString("Count"), input.CreateInt(1))
        });

    //castUnchecked reuses ExtraDataFixUtils.Cast for an unchecked type conversion
    private static Typed<object> CastUnchecked(Typed<object> input, T.Type<object> outputType)
        => ExtraDataFixUtils.Cast<object, object>(outputType, input);
}
