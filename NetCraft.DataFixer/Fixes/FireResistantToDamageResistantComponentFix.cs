using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//fire resistant component renamed to damage resistant component, maps to vanilla FireResistantToDamageResistantComponentFix
//1.21.4 changes minecraft:fire_resistant to minecraft:damage_resistant and its value structure to types=#minecraft:is_fire
public class FireResistantToDamageResistantComponentFix : DataComponentRemainderFix
{
    public FireResistantToDamageResistantComponentFix(Schema outputSchema)
        : base(outputSchema, "FireResistantToDamageResistantComponentFix", "minecraft:fire_resistant", "minecraft:damage_resistant") { }

    protected override Dynamic<object> FixComponent(Dynamic<object> input)
        => input.EmptyMap().Set("types", input.CreateString("#minecraft:is_fire"));
}
