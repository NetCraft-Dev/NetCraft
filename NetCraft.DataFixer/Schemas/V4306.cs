namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V4306 maps to vanilla net.minecraft.util.datafix.schemas.V4306
//1.21.3 removes the generic potion entity, splitting it into splash_potion/lingering_potion, both with an Item field
public class V4306 : NamespacedSchema
{
    public V4306(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        map.Remove("minecraft:potion");
        schema.Register(map, "minecraft:splash_potion", () => DSL.OptionalFields(FixConstants.EntityItem, References.ItemStack.In(schema)));
        schema.Register(map, "minecraft:lingering_potion", () => DSL.OptionalFields(FixConstants.EntityItem, References.ItemStack.In(schema)));
        return map;
    }
}
