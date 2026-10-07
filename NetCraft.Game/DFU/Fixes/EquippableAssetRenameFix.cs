using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Equippable asset rename, maps to vanilla EquippableAssetRenameFix
//1.21.4 renames the model field of the minecraft:equippable component to asset_id
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
