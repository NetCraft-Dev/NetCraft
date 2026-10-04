namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;

//V4307对应原版net.minecraft.util.datafix.schemas.V4307
//1.21.4覆写can_place_on/can_break改用adventureModePredicate支持单值或列表
public class V4307 : NamespacedSchema
{
    public V4307(int versionKey, Schema? parent) : base(versionKey, parent) { }

    //components复用V4059集替换can_place_on/can_break用冒险模式谓词
    public static Dictionary<string, Func<TypeTemplate>> Components(Schema schema)
    {
        var components = V4059.Components(schema);
        components["minecraft:can_place_on"] = () => AdventureModePredicate(schema);
        components["minecraft:can_break"] = () => AdventureModePredicate(schema);
        return components;
    }

    //adventureModePredicate构造单值或列表形式的方块谓词对齐原版adventureModePredicate
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
