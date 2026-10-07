using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Projectile stored item fix, maps to vanilla FixProjectileStoredItem
//1.20.5 adds an Item field to the trident/arrow/spectral_arrow entities; trident goes directly, arrow/spectral_arrow split by potion type
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

    //subFixer delegate signature returns a new Typed from the input Typed and output Type
    private delegate Typed<object> SubFixer(Typed<object> input, T.Type<object> outputType);

    //fixChoice builds a named choice lookup by entity name and applies the fix function
    private Func<Typed<object>, Typed<object>> FixChoice(string entityName, SubFixer fixer)
    {
        var inputEntityChoiceType = GetInputSchema().GetChoiceType(References.Entity, entityName);
        var outputEntityChoiceType = GetOutputSchema().GetChoiceType(References.Entity, entityName);
        var entityF = DSL.NamedChoice(entityName, inputEntityChoiceType);
        return input => input.UpdateTyped(entityF, outputEntityChoiceType, typed => fixer(typed, outputEntityChoiceType));
    }

    //fixArrow decides from the Potion field whether it is tipped_arrow and writes the Item field
    private static Typed<object> FixArrow(Typed<object> typed, T.Type<object> outputType)
        => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputType,
            input => input.Set(FixConstants.DecoratedPotBlockEntityItem, CreateItemStack(input, GetArrowType(input))));

    //getArrowType decides between arrow and tipped_arrow by whether the Potion field value is empty
    private static string GetArrowType(Dynamic<object> input)
        => input.Get("Potion").AsString(EMPTY_POTION).Equals(EMPTY_POTION) ? "minecraft:arrow" : "minecraft:tipped_arrow";

    //fixSpectralArrow writes the spectral_arrow's Item field
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

    //castUnchecked reuses ExtraDataFixUtils.Cast directly for an unchecked cast
    private static Typed<object> CastUnchecked(Typed<object> input, T.Type<object> outputType)
        => ExtraDataFixUtils.Cast<object, object>(outputType, input);
}
