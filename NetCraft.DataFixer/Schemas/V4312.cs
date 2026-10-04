namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//V4312对应原版net.minecraft.util.datafix.schemas.V4312
//1.21.4注册PLAYER类型组合装备+坐骑+末影珍珠+背包+末影箱+左右肩实体+配方书7字段
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
