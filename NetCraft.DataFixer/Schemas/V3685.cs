namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V3685对应原版net.minecraft.util.datafix.schemas.V3685
//1.20.5注册trident/spectral_arrow/arrow实体带inBlockState与item字段
public class V3685 : NamespacedSchema
{
    public V3685(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //abstractArrow箭类实体模板inBlockState+item字段对齐原版abstractArrow
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
