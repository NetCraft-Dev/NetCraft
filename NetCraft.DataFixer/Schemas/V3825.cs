namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V3825对应原版net.minecraft.util.datafix.schemas.V3825
//1.20.5注册ominous_item_spawner实体带item字段
public class V3825 : NamespacedSchema
{
    public V3825(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        Register(map, "minecraft:ominous_item_spawner", _ => DSL.OptionalFields(FixConstants.DecoratedPotBlockEntityItem, References.ItemStack.In(schema)));
        return map;
    }
}
