using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//equippable component asset rename, maps to vanilla EquippableAssetRenameFix
//1.21.4 renames the minecraft:equippable component's model field to asset_id
public class EquippableAssetRenameFix : DataFix
{
    public EquippableAssetRenameFix(Schema outputSchema) : base(outputSchema, true) { }

    protected override TypeRewriteRule MakeRule()
    {
        var componentsType = GetInputSchema().GetType(References.DataComponents);
        var equippableField = componentsType.FindField("minecraft:equippable");
        return FixTypeEverywhereTyped("equippable asset rename fix", componentsType, components =>
            components.UpdateTyped(equippableField, equippable =>
                equippable.Update(DSL.RemainderFinder(), tag => tag.RenameField("model", "asset_id"))));
    }
}
