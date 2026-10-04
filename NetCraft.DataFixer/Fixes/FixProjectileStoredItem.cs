using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//投射物存储物品修复对应原版FixProjectileStoredItem
//1.20.5为trident/arrow/spectral_arrow实体补充Item字段trident直转arrow/spectral_arrow按药水类型分流
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

    //subFixer委托签名按输入Typed与输出Type返回新Typed
    private delegate Typed<object> SubFixer(Typed<object> input, T.Type<object> outputType);

    //fixChoice按实体名构造命名选择查找并应用修复函数
    private Func<Typed<object>, Typed<object>> FixChoice(string entityName, SubFixer fixer)
    {
        var inputEntityChoiceType = GetInputSchema().GetChoiceType(References.Entity, entityName);
        var outputEntityChoiceType = GetOutputSchema().GetChoiceType(References.Entity, entityName);
        var entityF = DSL.NamedChoice(entityName, inputEntityChoiceType);
        return input => input.UpdateTyped(entityF, outputEntityChoiceType, typed => fixer(typed, outputEntityChoiceType));
    }

    //fixArrow按Potion字段判断是否tipped_arrow写入Item字段
    private static Typed<object> FixArrow(Typed<object> typed, T.Type<object> outputType)
        => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputType,
            input => input.Set(FixConstants.DecoratedPotBlockEntityItem, CreateItemStack(input, GetArrowType(input))));

    //getArrowType按Potion字段值是否为空判断arrow还是tipped_arrow
    private static string GetArrowType(Dynamic<object> input)
        => input.Get("Potion").AsString(EMPTY_POTION).Equals(EMPTY_POTION) ? "minecraft:arrow" : "minecraft:tipped_arrow";

    //fixSpectralArrow写入spectral_arrow的Item字段
    private static Typed<object> FixSpectralArrow(Typed<object> typed, T.Type<object> outputType)
        => DataFixUtils.WriteAndReadTypedOrThrow<object, object>(typed, outputType,
            input => input.Set(FixConstants.DecoratedPotBlockEntityItem, CreateItemStack(input, "minecraft:spectral_arrow")));

    //createItemStack构造{id:名字,Count:1}的物品map
    private static Dynamic<object> CreateItemStack(Dynamic<object> input, string itemName)
        => input.CreateMap(new[]
        {
            new Pair<Dynamic<object>, Dynamic<object>>(input.CreateString("id"), input.CreateString(itemName)),
            new Pair<Dynamic<object>, Dynamic<object>>(input.CreateString("Count"), input.CreateInt(1))
        });

    //castUnchecked直接复用ExtraDataFixUtils.Cast做未检查类型转换
    private static Typed<object> CastUnchecked(Typed<object> input, T.Type<object> outputType)
        => ExtraDataFixUtils.Cast<object, object>(outputType, input);
}
