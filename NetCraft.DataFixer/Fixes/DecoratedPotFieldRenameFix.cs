using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//decorated pot field rename fix, maps to vanilla DecoratedPotFieldRenameFix
//1.20.5 changes the decorated pot block_entity's item field name, going through an unchecked type conversion
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
