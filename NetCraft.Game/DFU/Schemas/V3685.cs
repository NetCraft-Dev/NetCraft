namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V3685 maps to vanilla net.minecraft.util.datafix.schemas.V3685
//1.20.5 registers the trident/spectral_arrow/arrow entities with inBlockState and item fields
public class V3685 : NamespacedSchema
{
    public V3685(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //abstractArrow arrow entity template with inBlockState+item fields, aligned with vanilla abstractArrow
    private static TypeTemplate AbstractArrow(Schema schema)
        => DSL.OptionalFields("inBlockState", References.BlockState.In(schema),
            FixConstants.DecoratedPotBlockEntityItem, References.ItemStack.In(schema));

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        Register(map, "minecraft:trident", _ => AbstractArrow(schema));
        Register(map, "minecraft:spectral_arrow", _ => AbstractArrow(schema));
        Register(map, "minecraft:arrow", _ => AbstractArrow(schema));
        return map;
    }
}
