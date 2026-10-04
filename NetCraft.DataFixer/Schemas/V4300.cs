namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V4300对应原版net.minecraft.util.datafix.schemas.V4300
//1.21.2拆分马匹实体llama/trader_llama/donkey/mule加Items字段
//horse/skeleton_horse/zombie_horse简化为无字段实体
public class V4300 : NamespacedSchema
{
    public V4300(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        schema.Register(map, "minecraft:llama", _ => EntityWithInventory(schema));
        schema.Register(map, "minecraft:trader_llama", _ => EntityWithInventory(schema));
        schema.Register(map, "minecraft:donkey", _ => EntityWithInventory(schema));
        schema.Register(map, "minecraft:mule", _ => EntityWithInventory(schema));
        schema.RegisterSimple(map, "minecraft:horse");
        schema.RegisterSimple(map, "minecraft:skeleton_horse");
        schema.RegisterSimple(map, "minecraft:zombie_horse");
        return map;
    }

    //entityWithInventory构造带Items字段列表的实体模板对应原版entityWithInventory
    public static TypeTemplate EntityWithInventory(Schema schema)
        => DSL.OptionalFields(FixConstants.ContainerHelperItems, DSL.List(References.ItemStack.In(schema)));
}
