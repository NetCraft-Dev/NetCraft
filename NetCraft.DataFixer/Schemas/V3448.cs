namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V3448 maps to vanilla net.minecraft.util.datafix.schemas.V3448
//1.20.3 registers the decorated_pot block entity with a sherds list and an item field
public class V3448 : NamespacedSchema
{
    public V3448(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override Dictionary<string, Func<TypeTemplate>> RegisterBlockEntities(Schema schema)
    {
        var map = base.RegisterBlockEntities(schema);
        Register(map, "minecraft:decorated_pot", _ => DSL.OptionalFields(
            FixConstants.DecoratedPotBlockEntitySherds, DSL.List(References.ItemName.In(schema)),
            FixConstants.DecoratedPotBlockEntityItem, References.ItemStack.In(schema)));
        return map;
    }
}
