namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V2505 maps to vanilla net.minecraft.util.datafix.schemas.V2505
//1.20.2 registers the piglin entity with an Inventory field list
public class V2505 : NamespacedSchema
{
    public V2505(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        Register(map, "minecraft:piglin", _ => DSL.OptionalFields(FixConstants.InventoryCarrierInventory, DSL.Remainder()));
        return map;
    }
}
