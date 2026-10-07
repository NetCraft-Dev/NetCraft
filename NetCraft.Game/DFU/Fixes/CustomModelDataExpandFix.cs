using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Custom model data expand fix, maps to vanilla CustomModelDataExpandFix
//1.21.4 wraps custom_model_data from a single float value into a {floats:[value]} structure
public class CustomModelDataExpandFix : DataFix
{
    public CustomModelDataExpandFix(Schema outputSchema) : base(outputSchema, false) { }

    protected override TypeRewriteRule MakeRule()
    {
        var componentsType = GetInputSchema().GetType(References.DataComponents);
        return FixTypeEverywhereTyped("Custom Model Data expansion", componentsType, component =>
            component.Update(DSL.RemainderFinder(), tag =>
                tag.Update("minecraft:custom_model_data", cmd =>
                {
                    float currentValue = cmd.AsFloat(0.0f);
                    return cmd.CreateMap(new[]
                    {
                        new Pair<Dynamic<object>, Dynamic<object>>(
                            cmd.CreateString("floats"),
                            cmd.CreateList(new[] { cmd.CreateFloat(currentValue) }))
                    });
                })));
    }
}
