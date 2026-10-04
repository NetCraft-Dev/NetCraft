using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//食物转可食用组件修复对应原版FoodToConsumableFix
//1.21.2拆分food组件为food/use_remainder/consumable三组件并把effects转为apply_effects效果
public class FoodToConsumableFix : DataFix
{
    public FoodToConsumableFix(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
        => WriteFixAndRead("Food to consumable fix",
            GetInputSchema().GetType(References.DataComponents),
            GetOutputSchema().GetType(References.DataComponents),
            components =>
            {
                var foodComponentOpt = components.Get("minecraft:food").Result();
                if (!foodComponentOpt.IsPresent) return components;
                var foodComponent = foodComponentOpt.Get();
                float eatSeconds = foodComponent.Get("eat_seconds").AsFloat(1.6f);
                var effects = foodComponent.Get("effects").AsStream().Result().OrElse(Enumerable.Empty<Dynamic<object>>());
                var mappedEffects = effects.Select(effect =>
                    effect.EmptyMap()
                        .Set(FixConstants.ChunkRegionIoEventType, effect.CreateString("minecraft:apply_effects"))
                        .Set("effects", effect.CreateList(effect.Get("effect").Result().Stream()))
                        .Set("probability", effect.CreateFloat(effect.Get("probability").AsFloat(1.0f)))).ToList();
                var newComponents = Dynamic<object>.CopyField(foodComponent, FixConstants.FoodUsingConvertsTo, components, "minecraft:use_remainder")
                    .Set("minecraft:food", foodComponent.Remove("eat_seconds").Remove("effects").Remove(FixConstants.FoodUsingConvertsTo));
                return newComponents.Set("minecraft:consumable",
                    newComponents.EmptyMap()
                        .Set("consume_seconds", newComponents.CreateFloat(eatSeconds))
                        .Set("on_consume_effects", newComponents.CreateList(mappedEffects)));
            });
}
