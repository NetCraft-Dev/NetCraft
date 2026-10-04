using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.DataFixer.Fixes;

//自定义模型数据扩展修复对应原版CustomModelDataExpandFix
//1.21.4把custom_model_data从单float值包装为{floats:[value]}结构
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
