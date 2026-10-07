namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V4307 maps to vanilla net.minecraft.util.datafix.schemas.V4307
//1.21.4 overrides can_place_on/can_break to use adventureModePredicate, supporting a single value or a list
public class V4307 : NamespacedSchema
{
    public V4307(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //components reuses the V4059 set, replacing can_place_on/can_break with the adventure mode predicate
    public static Dictionary<string, Func<TypeTemplate>> Components(Schema schema)
    {
        var components = V4059.Components(schema);
        components["minecraft:can_place_on"] = () => AdventureModePredicate(schema);
        components["minecraft:can_break"] = () => AdventureModePredicate(schema);
        return components;
    }

    //adventureModePredicate builds the block predicate in single-value or list form, aligned with vanilla adventureModePredicate
    static TypeTemplate AdventureModePredicate(Schema schema)
    {
        var predicate = DSL.OptionalFields(FixConstants.StructureTemplateBlocks,
            DSL.Or(References.BlockName.In(schema), DSL.List(References.BlockName.In(schema))));
        return DSL.Or(predicate, DSL.List(predicate));
    }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        schema.RegisterType(true, References.DataComponents, () => DSL.OptionalFieldsLazy(Components(schema)));
    }
}
