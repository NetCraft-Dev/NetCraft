namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V3448对应原版net.minecraft.util.datafix.schemas.V3448
//1.20.3注册decorated_pot方块实体带sherds列表与item字段
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
