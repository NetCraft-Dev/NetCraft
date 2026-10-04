using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//鞍具装备槽修复对应原版SaddleEquipmentSlotFix
//1.21.4为带鞍生物马匹类把SaddleItem/Saddle标志统一为saddle物品字段并写入DropChances
//带SaddleItem实体重命名为saddleSaddle标志实体构造saddle物品
public class SaddleEquipmentSlotFix : DataFix
{
    private static readonly HashSet<string> ENTITIES_WITH_SADDLE_ITEM = new()
    {
        "minecraft:horse", "minecraft:skeleton_horse", "minecraft:zombie_horse",
        "minecraft:donkey", "minecraft:mule", "minecraft:camel",
        "minecraft:llama", "minecraft:trader_llama"
    };
    private static readonly HashSet<string> ENTITIES_WITH_SADDLE_FLAG = new() { "minecraft:pig", "minecraft:strider" };
    private const string SADDLE_FLAG = "Saddle";
    private const string NEW_SADDLE = "saddle";
    private const string ROOT_LOCALE = "";

    public SaddleEquipmentSlotFix(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
    {
        var entityIdType = GetInputSchema().FindChoiceType(References.Entity);
        var entityIdF = DSL.TypeFinder(entityIdType);
        var inputType = GetInputSchema().GetType(References.Entity);
        var outputType = GetOutputSchema().GetType(References.Entity);
        var patchedInputType = ExtraDataFixUtils.PatchSubType(inputType, inputType, outputType);
        return FixTypeEverywhereTyped("SaddleEquipmentSlotFix", inputType, outputType, input =>
        {
            var entityId = input.GetOptional(entityIdF)
                .Map(v => (string)((NetCraft.DataFixer.Util.Pair<object, object>)v).First)
                .Map(NamespacedSchema.EnsureNamespaced)
                .OrElse(ROOT_LOCALE);
            var fixedInput = ExtraDataFixUtils.Cast<object, object>(patchedInputType, input);
            if (ENTITIES_WITH_SADDLE_ITEM.Contains(entityId))
            {
                return DataFixUtils.WriteAndReadTypedOrThrow<object, object>(fixedInput, outputType, FixEntityWithSaddleItem);
            }
            if (ENTITIES_WITH_SADDLE_FLAG.Contains(entityId))
            {
                return DataFixUtils.WriteAndReadTypedOrThrow<object, object>(fixedInput, outputType, FixEntityWithSaddleFlag);
            }
            return ExtraDataFixUtils.Cast<object, object>(outputType, input);
        });
    }

    //fixEntityWithSaddleItem把SaddleItem重命名为saddle并补充DropChances
    private static Dynamic<object> FixEntityWithSaddleItem(Dynamic<object> input)
    {
        if (!input.Get("SaddleItem").Result().IsPresent) return input;
        return FixDropChances(input.RenameField("SaddleItem", NEW_SADDLE));
    }

    //fixEntityWithSaddleFlag按Saddle标志构造saddle物品并补充DropChances
    private static Dynamic<object> FixEntityWithSaddleFlag(Dynamic<object> tag)
    {
        bool hasSaddle = tag.Get(SADDLE_FLAG).AsBoolean(false);
        var tag2 = tag.Remove(SADDLE_FLAG);
        if (!hasSaddle) return tag2;
        var saddleItem = tag2.EmptyMap()
            .Set("id", tag2.CreateString("minecraft:saddle"))
            .Set(FixConstants.ItemInstanceCount, tag2.CreateInt(1));
        return FixDropChances(tag2.Set(NEW_SADDLE, saddleItem));
    }

    //fixDropChances在DropChances map写入saddle槽位固定几率2.0f
    private static Dynamic<object> FixDropChances(Dynamic<object> tag)
    {
        var dropChances = tag.Get(FixConstants.MobDropChances).Result().OrElse(tag.EmptyMap()).Set(NEW_SADDLE, tag.CreateFloat(2.0f));
        return tag.Set(FixConstants.MobDropChances, dropChances);
    }
}
