using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//掉落几率格式修复对应原版DropChancesFormatFix
//1.21.4把ArmorDropChances/HandDropChances/body_armor_drop_chance合并为DropChances map字段
//默认几率0.085f不变的不写入map
public class DropChancesFormatFix : DataFix
{
    private const float DEFAULT_CHANCE = 0.085f;
    private static readonly string[] ARMOR_SLOT_NAMES = { FixConstants.PartNameFeet, "legs", "chest", FixConstants.PartNameHead };
    private static readonly string[] HAND_SLOT_NAMES = { "mainhand", "offhand" };

    public DropChancesFormatFix(Schema outputSchema) : base(outputSchema, false) { }

    protected override TypeRewriteRule MakeRule()
        => FixTypeEverywhereTyped("DropChancesFormatFix", GetInputSchema().GetType(References.Entity), input =>
            input.Update(DSL.RemainderFinder(), remainder =>
            {
                var armorDropChances = ParseDropChances(remainder.Get("ArmorDropChances"));
                var handDropChances = ParseDropChances(remainder.Get("HandDropChances"));
                float bodyArmorDropChance = (float)remainder.Get("body_armor_drop_chance").AsNumber().Result().Map(v => (float)v).OrElse(DEFAULT_CHANCE);
                var newRemainder = remainder.Remove("ArmorDropChances").Remove("HandDropChances").Remove("body_armor_drop_chance");
                var slotChances = AddSlotChances(AddSlotChances(newRemainder.EmptyMap(), armorDropChances, ARMOR_SLOT_NAMES), handDropChances, HAND_SLOT_NAMES);
                if (bodyArmorDropChance != DEFAULT_CHANCE)
                {
                    slotChances = slotChances.Set(FixConstants.PartNameBody, newRemainder.CreateFloat(bodyArmorDropChance));
                }
                if (!slotChances.Equals(newRemainder.EmptyMap()))
                {
                    return newRemainder.Set(FixConstants.MobDropChances, slotChances);
                }
                return newRemainder;
            }));

    //addSlotChances按slotNames与chances对齐写入非默认值到output map
    private static Dynamic<object> AddSlotChances(Dynamic<object> output, List<float> chances, string[] slotNames)
    {
        for (int i = 0; i < slotNames.Length && i < chances.Count; i++)
        {
            float chance = chances[i];
            if (chance != DEFAULT_CHANCE)
            {
                output = output.Set(slotNames[i], output.CreateFloat(chance));
            }
        }
        return output;
    }

    //parseDropChances从OptionalDynamic读列表每个元素转float默认0.085f
    private static List<float> ParseDropChances(OptionalDynamic<object> value)
        => value.AsStream().Result().OrElse(Enumerable.Empty<Dynamic<object>>())
            .Select(d => d.AsFloat(DEFAULT_CHANCE)).ToList();
}
