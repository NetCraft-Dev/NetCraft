namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//V3938 maps to vanilla net.minecraft.util.datafix.schemas.V3938
//1.21 registers the spectral_arrow/arrow entities with inBlockState+item+weapon fields
public class V3938 : NamespacedSchema
{
    public V3938(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //abstractArrow arrow-class entity template with inBlockState+item+weapon fields, aligning with vanilla abstractArrow
    private static TypeTemplate AbstractArrow(Schema schema)
        => DSL.OptionalFields(
            new Pair<string, TypeTemplate>("inBlockState", References.BlockState.In(schema)),
            new Pair<string, TypeTemplate>(FixConstants.DecoratedPotBlockEntityItem, References.ItemStack.In(schema)),
            new Pair<string, TypeTemplate>("weapon", References.ItemStack.In(schema)));

    public override Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
    {
        var map = base.RegisterEntities(schema);
        Register(map, "minecraft:spectral_arrow", _ => AbstractArrow(schema));
        Register(map, "minecraft:arrow", _ => AbstractArrow(schema));
        return map;
    }
}
