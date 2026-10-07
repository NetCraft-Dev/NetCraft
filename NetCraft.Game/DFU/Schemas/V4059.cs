namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V4059 maps to vanilla net.minecraft.util.datafix.schemas.V4059
//On top of the V3818_3 component set it removes food and adds use_remainder/equippable/sulfur_cube_content
public class V4059 : NamespacedSchema
{
    public V4059(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //components reuses the V3818_3 set, removes food and adds 3 components, aligned with the vanilla override
    public static Dictionary<string, Func<TypeTemplate>> Components(Schema schema)
    {
        var components = V3818_3.Components(schema);
        components.Remove("minecraft:food");
        components["minecraft:use_remainder"] = () => References.ItemStack.In(schema);
        components["minecraft:equippable"] = () => DSL.OptionalFields("allowed_entities",
            DSL.Or(References.EntityName.In(schema), DSL.List(References.EntityName.In(schema))));
        components["minecraft:sulfur_cube_content"] = () => References.ItemStack.In(schema);
        return components;
    }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        schema.RegisterType(true, References.DataComponents, () => DSL.OptionalFieldsLazy(Components(schema)));
    }
}
