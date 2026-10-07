namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Schemas;

//MC data fixer entry point maps to vanilla net.minecraft.util.datafix.DataFixers
//the DFU kernel layer only provides the framework and does not register any Schema or Fix itself
//concrete Schema and Fix registration is done by the business layer (NetCraft.Game) calling DataFixerBuilder.AddSchema/AddFixer
//mirrors the vanilla buildFixer, registering 1000+ Schemas and Fixes in the Game layer
public static class DataFixers
{
    //DFU does not maintain a global DataFixer instance
    //the business layer constructs a DataFixerBuilder itself and calls Build().Fixer() to get a DataFixer instance
    //example usage:
    //var builder = new DataFixerBuilder(dataVersion);
    //builder.AddSchema(version, subVersion, (key, parent) => new SomeSchema(key, parent));
    //builder.AddFixer(new SomeFix(outputSchema));
    //var fixer = builder.Build().Fixer();
}
