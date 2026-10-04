namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V3818_3对应原版net.minecraft.util.datafix.schemas.V3818_3
//注册1.20.5数据组件模板DATA_COMPONENTS覆盖13个组件类型
//后续V4059/V4307继承覆写components方法扩展组件集
public class V3818_3 : NamespacedSchema
{
    public V3818_3(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //components构造13个数据组件模板Map供DATA_COMPONENTS注册用
    //静态方法让V4059/V4307继承复用并扩展remove/put
    public static Dictionary<string, Func<TypeTemplate>> Components(Schema schema)
    {
        var map = new Dictionary<string, Func<TypeTemplate>>();
        map["minecraft:bees"] = () => DSL.List(DSL.OptionalFields("entity_data", References.EntityTree.In(schema)));
        map["minecraft:block_entity_data"] = () => References.BlockEntity.In(schema);
        map["minecraft:bundle_contents"] = () => DSL.List(References.ItemStack.In(schema));
        map["minecraft:can_break"] = () => DSL.OptionalFields("predicates",
            DSL.List(DSL.OptionalFields(FixConstants.StructureTemplateBlocks,
                DSL.Or(References.BlockName.In(schema), DSL.List(References.BlockName.In(schema))))));
        map["minecraft:can_place_on"] = () => DSL.OptionalFields("predicates",
            DSL.List(DSL.OptionalFields(FixConstants.StructureTemplateBlocks,
                DSL.Or(References.BlockName.In(schema), DSL.List(References.BlockName.In(schema))))));
        map["minecraft:charged_projectiles"] = () => DSL.List(References.ItemStack.In(schema));
        map["minecraft:container"] = () => DSL.List(DSL.OptionalFields(FixConstants.DecoratedPotBlockEntityItem, References.ItemStack.In(schema)));
        map["minecraft:entity_data"] = () => References.EntityTree.In(schema);
        map["minecraft:pot_decorations"] = () => DSL.List(References.ItemName.In(schema));
        map["minecraft:food"] = () => DSL.OptionalFields(FixConstants.FoodUsingConvertsTo, References.ItemStack.In(schema));
        map["minecraft:custom_name"] = () => References.TextComponent.In(schema);
        map["minecraft:item_name"] = () => References.TextComponent.In(schema);
        map["minecraft:lore"] = () => DSL.List(References.TextComponent.In(schema));
        map["minecraft:written_book_content"] = () => DSL.OptionalFields(FixConstants.WrittenBookPages,
            DSL.List(DSL.Or(
                DSL.OptionalFields(FixConstants.WrittenBookRaw, References.TextComponent.In(schema),
                    FixConstants.WrittenBookFiltered, References.TextComponent.In(schema)),
                References.TextComponent.In(schema))));
        return map;
    }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        schema.RegisterType(true, References.DataComponents, () => DSL.OptionalFieldsLazy(Components(schema)));
    }
}
