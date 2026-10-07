namespace NetCraft.Game.DFU.Schemas;

using NetCraft.DataFixer;

using NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Types.Templates;
using NetCraft.DataFixer.Util;

//V4312 maps to vanilla net.minecraft.util.datafix.schemas.V4312
//1.21.4 registers the PLAYER type, the vanilla 7-field AND template
//End-to-end tests simplify PLAYER with Remainder to avoid And(Product TypeTemplate Apply producing a SumType that fails the Type<object> cast
public class V4312 : NamespacedSchema
{
    public V4312(int versionKey, Schema? parent) : base(versionKey, parent) { }

    public override void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
    {
        base.RegisterTypes(schema, entityTypes, blockEntityTypes);
        //PLAYER uses Remainder passthrough; the Update flow does not take part in field-level fixes
        //Vanilla AND+OptionalFields produces a SumType after TypeTemplate.Apply that fails the Type<object> cast
        //When the business layer later needs field-level PLAYER fixes, switch back to the vanilla AND structure
        schema.RegisterType(false, References.Player, () => DSL.Remainder());
    }
}
