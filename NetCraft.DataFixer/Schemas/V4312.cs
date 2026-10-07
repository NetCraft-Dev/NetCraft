namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//V4312 maps to vanilla net.minecraft.util.datafix.schemas.V4312
//1.21.4 registers the PLAYER type combining equipment+mount+ender_pearl+inventory+ender_chest+left/right shoulder entity+recipe book, 7 fields
public class V4312 : NamespacedSchema
{
    public V4312(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        schema.RegisterType(false, References.Player, () => DSL.And(
            References.EntityEquipment.In(schema),
            DSL.OptionalFields(
                new Pair<string, TypeTemplate>(FixConstants.PlayerRootVehicle, DSL.OptionalFields("Entity", References.EntityTree.In(schema))),
                new Pair<string, TypeTemplate>(FixConstants.ServerPlayerEnderPearls, DSL.List(References.EntityTree.In(schema))),
                new Pair<string, TypeTemplate>(FixConstants.InventoryCarrierInventory, DSL.List(References.ItemStack.In(schema))),
                new Pair<string, TypeTemplate>(FixConstants.PlayerEnderItems, DSL.List(References.ItemStack.In(schema))),
                new Pair<string, TypeTemplate>(FixConstants.PlayerShoulderEntityLeft, References.EntityTree.In(schema)),
                new Pair<string, TypeTemplate>(FixConstants.PlayerShoulderEntityRight, References.EntityTree.In(schema)),
                new Pair<string, TypeTemplate>(FixConstants.ServerRecipeBookRecipeBook,
                    DSL.OptionalFields(FixConstants.RecipeBookRecipes, DSL.List(References.Recipe.In(schema)),
                        FixConstants.RecipeBookToBeDisplayed, DSL.List(References.Recipe.In(schema)))))));
    }
}
