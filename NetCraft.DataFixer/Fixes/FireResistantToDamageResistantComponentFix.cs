using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//火焰抗性组件重命名为伤害抗性组件对应原版FireResistantToDamageResistantComponentFix
//1.21.4把minecraft:fire_resistant改为minecraft:damage_resistant并改值结构types=#minecraft:is_fire
public class FireResistantToDamageResistantComponentFix : DataComponentRemainderFix
{
    public FireResistantToDamageResistantComponentFix(Schema outputSchema)
        : base(outputSchema, "FireResistantToDamageResistantComponentFix", "minecraft:fire_resistant", "minecraft:damage_resistant") { }

    protected override Dynamic<object> FixComponent(Dynamic<object> input)
        => input.EmptyMap().Set("types", input.CreateString("#minecraft:is_fire"));
}
