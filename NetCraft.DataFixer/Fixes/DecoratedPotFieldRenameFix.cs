using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//饰纹罐字段重命名修复对应原版DecoratedPotFieldRenameFix
//1.20.5装饰花瓶block_entity的item字段名变化走未检查类型转换
public class DecoratedPotFieldRenameFix : DataFix
{
    private const string DECORATED_POT_ID = "minecraft:decorated_pot";

    public DecoratedPotFieldRenameFix(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
    {
        var oldDecoratedPot = GetInputSchema().GetChoiceType(References.BlockEntity, DECORATED_POT_ID);
        var newDecoratedPot = GetOutputSchema().GetChoiceType(References.BlockEntity, DECORATED_POT_ID);
        return ConvertUnchecked("DecoratedPotFieldRenameFix", oldDecoratedPot, newDecoratedPot);
    }
}
