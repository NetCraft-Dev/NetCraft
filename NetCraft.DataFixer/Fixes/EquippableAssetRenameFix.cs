using NetCraft.DataFixer.Schemas;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer.Fixes;

//可装备组件资源重命名对应原版EquippableAssetRenameFix
//1.21.4把minecraft:equippable组件的model字段重命名为asset_id
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
